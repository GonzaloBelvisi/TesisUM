using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using SitradWebInterface.Models;

namespace SitradWebInterface.Controllers
{
    public class SitradController : Controller
    {
        private const string UserIdSessionKey = "UserId";
        private const string UserRoleSessionKey = "UserRole";
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        
        // Sistema de reintentos para setpoints
        private static readonly Dictionary<string, SetpointRetryInfo> _retryQueue = new();
        private static readonly object _retryLock = new object();

        public SitradController(IConfiguration configuration)
        {
            _configuration = configuration;
            
            // Omitir validación de certificado (solo en desarrollo)
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, __, ___, ____) => true
            };
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            // Leer credenciales de configuración o variables de entorno
            var username = _configuration["SitradApi:Username"] 
                          ?? Environment.GetEnvironmentVariable("SITRAD_API_USERNAME") 
                          ?? "";
            var password = _configuration["SitradApi:Password"] 
                          ?? Environment.GetEnvironmentVariable("SITRAD_API_PASSWORD") 
                          ?? "";
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
        }

        private bool IsAuthenticated() =>
            HttpContext.Session.GetInt32(UserIdSessionKey).HasValue;

        private UserRole? GetUserRole()
        {
            var roleString = HttpContext.Session.GetString(UserRoleSessionKey);
            if (Enum.TryParse<UserRole>(roleString, out var role))
            {
                return role;
            }
            return null;
        }

        private bool IsOperario() => GetUserRole() == UserRole.Operario;

        // GET: /Sitrad
        public async Task<IActionResult> Index()
        {
            if (!IsAuthenticated())
                return RedirectToAction("Login", "Account");

            var data = await GetDashboardDataInternal();
            ViewData["UserRole"] = GetUserRole()?.ToString() ?? "Visualizador";
            ViewData["IsOperario"] = IsOperario();
            return View(data);
        }

        // GET AJAX: /Sitrad/GetDashboardData
        [HttpGet]
        public async Task<IActionResult> GetDashboardData()
        {
            if (!IsAuthenticated())
                return Unauthorized();

            var data = await GetDashboardDataInternal();
            return Json(data);
        }

        // POST: /Sitrad/UpdateFunctionValue
        [HttpPost]
        public async Task<IActionResult> UpdateFunctionValue(int instrumentId, string code, double newValue)
        {
            if (!IsAuthenticated())
                return Unauthorized();

            // Solo los operarios pueden modificar valores
            if (!IsOperario())
            {
                return Json(new
                {
                    success = false,
                    message = "No tiene permisos para modificar valores. Solo los operarios pueden realizar cambios."
                });
            }

            // Modo demo: no llamar a la API real. Respondemos éxito inmediato.
            return Json(new
            {
                success = true,
                message = "Valor actualizado",
                appliedValue = newValue,
                code = code?.ToUpperInvariant()
            });
        }

        // GET: /Sitrad/GetRetryStatus
        [HttpGet]
        public IActionResult GetRetryStatus()
        {
            if (!IsAuthenticated())
                return Unauthorized();

            lock (_retryLock)
            {
                var retryInfo = _retryQueue.Values.Select(r => new
                {
                    instrumentId = r.InstrumentId,
                    code = r.Code,
                    value = r.Value,
                    attempts = r.Attempts,
                    lastAttempt = r.LastAttempt
                }).ToList();

                return Json(new { retries = retryInfo });
            }
        }

        // ——————————————————————————————————————————————
        // Lógica para leer cámaras + nuevos instrumentos de pulpa
        private async Task<List<CameraViewModel>> GetDashboardDataInternal()
        {
            var apiBaseUrl = Environment.GetEnvironmentVariable("SITRAD_API_URL")
                             ?? "https://200.40.68.62:20108/api/v1";

            // 1) Obtener todos los instrumentos activos
            var instrumentsUrl = $"{apiBaseUrl}/instruments?instrumentStatus=active";
            HttpResponseMessage instrResp;
            try
            {
                instrResp = await _httpClient.GetAsync(instrumentsUrl);
            }
            catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException)
            {
                return new List<CameraViewModel>();
            }
            if (!instrResp.IsSuccessStatusCode)
                return new List<CameraViewModel>();

            var instrJson = await instrResp.Content.ReadAsStringAsync();
            var instrContainer = JsonConvert.DeserializeObject<InstrumentResponse>(instrJson);
            var instrumentList = instrContainer.results;

            // 2) Agrupar por número de cámara:
            //    baseInst = "Camara X - Zac", pulpInst = "Temperatura Pulpa Cam X"
            var cameraGroups = new Dictionary<string, (InstrumentModel baseInst, InstrumentModel pulpInst)>();
            foreach (var inst in instrumentList)
            {
                var name = inst.name ?? "";
                // extraigo el número (último token antes de guión o al final)
                var key = name.Split('-', 2)[0]
                              .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                              .LastOrDefault() ?? name;

                if (!cameraGroups.ContainsKey(key))
                    cameraGroups[key] = (null, null);

                if (name.StartsWith("Camara", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Cámara", StringComparison.OrdinalIgnoreCase))
                {
                    cameraGroups[key] = (inst, cameraGroups[key].pulpInst);
                }
                else if (name.StartsWith("Temperatura Pulpa", StringComparison.OrdinalIgnoreCase))
                {
                    cameraGroups[key] = (cameraGroups[key].baseInst, inst);
                }
            }

            var vmList = new List<CameraViewModel>();

            // 3) Para cada cámara, leo valores de baseInst y pulpInst
            foreach (var kvp in cameraGroups)
            {
                var baseInst = kvp.Value.baseInst;
                var pulpInst = kvp.Value.pulpInst;

                double? tempMain = null;
                double? hum = null;
                double? s1 = null;
                double? s3 = null;
                double? pulpTemp = null;
                double? evapTemp = null;

                // a) datos de la cámara base (temperatura, humedad, setpoints)
                if (baseInst != null)
                {
                    // — valores actuales
                    var valUrl = $"{apiBaseUrl}/instruments/{baseInst.id}/values";
                    var valResp = await _httpClient.GetAsync(valUrl);
                    if (valResp.IsSuccessStatusCode)
                    {
                        var vc = JsonConvert.DeserializeObject<InstrumentValuesResponse>(
                                     await valResp.Content.ReadAsStringAsync());

                        // Temperatura simple
                        var grpT = vc.results
                                     .FirstOrDefault(g =>
                                         g.code.Equals("Temperature", StringComparison.OrdinalIgnoreCase));
                        if (grpT?.values?.Any() == true &&
                            double.TryParse(grpT.values[0].value?.ToString(), out var tm))
                        {
                            tempMain = tm;
                        }

                        // Humedad
                        var grpH = vc.results
                                     .FirstOrDefault(g =>
                                         g.code.Equals("Humidity", StringComparison.OrdinalIgnoreCase));
                        if (grpH?.values?.Any() == true &&
                            double.TryParse(grpH.values[0].value?.ToString(), out var hh))
                        {
                            hum = hh;
                        }
                    }

                    // — setpoints SET1 / SET3
                    var fnUrl = $"{apiBaseUrl}/instruments/{baseInst.id}/functions";
                    var fnResp = await _httpClient.GetAsync(fnUrl);
                    if (fnResp.IsSuccessStatusCode)
                    {
                        var fc = JsonConvert.DeserializeObject<InstrumentFunctionsResponse>(
                                     await fnResp.Content.ReadAsStringAsync());
                        foreach (var fn in fc.results)
                        {
                            if (fn.code.Equals("SET1", StringComparison.OrdinalIgnoreCase))
                                s1 = fn.value;
                            if (fn.code.Equals("SET3", StringComparison.OrdinalIgnoreCase))
                                s3 = fn.value;
                        }
                    }
                }

                // …
                if (pulpInst != null)
                {
                    var valUrl = $"{apiBaseUrl}/instruments/{pulpInst.id}/values";
                    var valResp = await _httpClient.GetAsync(valUrl);
                    if (valResp.IsSuccessStatusCode)
                    {
                        var vc = JsonConvert.DeserializeObject<InstrumentValuesResponse>(
                                     await valResp.Content.ReadAsStringAsync());

                        // --- Temperatura de Pulpa ---
                        var grpPulpa = vc.results
                            .FirstOrDefault(g =>
                                g.code.Equals("Sensor1", StringComparison.OrdinalIgnoreCase)
                             || (g.name ?? "").IndexOf("Pulpa", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (grpPulpa?.values?.Any() == true
                            && double.TryParse(grpPulpa.values[0].value?.ToString(), out var p))
                            pulpTemp = p;

                        // --- Temperatura del Evaporador ---
                        var grpEvap = vc.results
                            .FirstOrDefault(g =>
                                g.code.Equals("Sensor2", StringComparison.OrdinalIgnoreCase)
                             || (g.name ?? "").IndexOf("Evaporador", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (grpEvap?.values?.Any() == true
                            && double.TryParse(grpEvap.values[0].value?.ToString(), out var e))
                            evapTemp = e;
                    }
                }
            


                vmList.Add(new CameraViewModel
                {
                    InstrumentId = baseInst?.id ?? pulpInst?.id ?? 0,
                    CameraName = baseInst?.name ?? pulpInst?.name ?? kvp.Key,
                    Temperature = tempMain.HasValue ? $"{tempMain.Value:N1}°C" : "N/A",
                    Humidity = hum.HasValue ? $"{hum.Value:N1}%" : "N/A",
                    PulpTemp = pulpTemp.HasValue ? $"{pulpTemp.Value:N1}°C" : "N/A",
                    EvaporatorTemp = evapTemp.HasValue ? $"{evapTemp.Value:N1}°C" : "N/A",
                    Set1 = s1.HasValue ? $"{s1.Value:N1}°C" : "N/A",
                    Set3 = s3.HasValue ? $"{s3.Value:N1}°C" : "N/A",
                    co2 = "N/A",
                    etileno = "N/A"
                });
            }

            // 4) Lectura de gas externo
            string gasApi = "http://111.231.172.136:8081/api/platform/basicOpenapi/getDeviceListRealTimeData"
                          + "?appKey=nYVcDkxB&appSecret=4894a42e27eb4ac694d700c8ce8624fe";
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var gResp = await _httpClient.GetAsync(gasApi, cts.Token);
                if (gResp.IsSuccessStatusCode)
                {
                    var gd = JsonConvert.DeserializeObject<GasSensorResponse>(
                        await gResp.Content.ReadAsStringAsync());
                    var co2Map = new Dictionary<string, string>();
                    var ethMap = new Dictionary<string, string>();
                    var gasOnlyCameras = new HashSet<string>(); // Para cámaras que solo tienen sensores de gas

                    // Mapeo de gas
                    foreach (var item in gd.data)
                    {
                        var alias = item.imeiAlias ?? "";
                        var key = alias.Contains("CO2_Camara_")
                                  ? alias["CO2_Camara_".Length..]
                                  : alias.Contains("Etileno_Camara_")
                                    ? alias["Etileno_Camara_".Length..]
                                    : null;
                        var val = item.sensorTransmissionData?
                                        .sensorTransmissionDataDetailsList?
                                        .FirstOrDefault()?
                                        .sensorVal;
                        if (key != null && !string.IsNullOrEmpty(val))
                        {
                            if (alias.StartsWith("CO2_")) 
                            {
                                co2Map[key] = val;
                                gasOnlyCameras.Add(key);
                            }
                            if (alias.StartsWith("Etileno_")) 
                            {
                                ethMap[key] = val;
                                gasOnlyCameras.Add(key);
                            }
                        }
                    }

                    // Crear entradas para cámaras que solo tienen sensores de gas
                    foreach (var camNum in gasOnlyCameras)
                    {
                        // Verificar si ya existe una entrada para esta cámara
                        var existingCam = vmList.FirstOrDefault(vm => 
                        {
                            var camName = vm.CameraName;
                            var beforeDashCam = camName.Contains('-')
                                ? camName.Split('-', 2)[0]
                                : camName;
                            var tokensCam = beforeDashCam
                                .Trim()
                                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            var num = tokensCam.Last();
                            return num == camNum || num == camNum.PadLeft(2, '0');
                        });

                        if (existingCam == null)
                        {
                            // Crear nueva entrada para cámara que solo tiene sensores de gas
                            vmList.Add(new CameraViewModel
                            {
                                InstrumentId = 0, // No tiene instrumento base
                                CameraName = $"Camara {camNum} - Gas",
                                Temperature = "N/A",
                                Humidity = "N/A",
                                PulpTemp = "N/A",
                                EvaporatorTemp = "N/A",
                                Set1 = "N/A",
                                Set3 = "N/A",
                                co2 = co2Map.TryGetValue(camNum, out var c)
                                    ? $"{c} %VOL"
                                    : co2Map.TryGetValue(camNum.PadLeft(2, '0'), out var c2)
                                        ? $"{c2} %VOL"
                                        : "N/A",
                                etileno = ethMap.TryGetValue(camNum, out var e)
                                    ? $"{e} ppm"
                                    : ethMap.TryGetValue(camNum.PadLeft(2, '0'), out var e2)
                                        ? $"{e2} ppm"
                                        : "N/A"
                            });
                        }
                    }

                    // Asignación de gas con extracción del número de cámara para cámaras existentes
                    foreach (var vm in vmList)
                    {
                        var camName = vm.CameraName;
                        var beforeDashCam = camName.Contains('-')
                            ? camName.Split('-', 2)[0]
                            : camName;
                        var tokensCam = beforeDashCam
                            .Trim()
                            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var num = tokensCam.Last();
                        var pad = num.PadLeft(2, '0');

                        // Solo actualizar si no es una cámara de gas que ya tiene valores asignados
                        if (!vm.CameraName.Contains("Gas"))
                        {
                            vm.co2 = co2Map.TryGetValue(num, out var c)
                                ? $"{c} %VOL"
                                : co2Map.TryGetValue(pad, out var c2)
                                    ? $"{c2} %VOL"
                                    : "N/A";

                            vm.etileno = ethMap.TryGetValue(num, out var e)
                                ? $"{e} ppm"
                                : ethMap.TryGetValue(pad, out var e2)
                                    ? $"{e2} ppm"
                                    : "N/A";
                        }
                    }
                }
            }
            catch { /* ignorar timeouts */ }

            return vmList;
        }


        #region Clases para Deserialización de API

        public class InstrumentResponse
        {
            public int resultsQty { get; set; }
            public List<InstrumentModel> results { get; set; }
            public int status { get; set; }
        }

        public class InstrumentModel
        {
            public int id { get; set; }
            public int converterId { get; set; }
            public string name { get; set; }
            public int address { get; set; }
            public int statusId { get; set; }
            public string status { get; set; }
            public int modelId { get; set; }
            public int modelVersion { get; set; }
            public bool isAlarmsManuallyInhibited { get; set; }
        }

        public class InstrumentValuesResponse
        {
            public int resultsQty { get; set; }
            public List<InstrumentValueGroup> results { get; set; }
            public int status { get; set; }
        }

        public class InstrumentValueGroup
        {
            public string code { get; set; }
            public string name { get; set; }
            public List<InstrumentValue> values { get; set; }
        }

        public class InstrumentValue
        {
            public string date { get; set; }
            public object value { get; set; }
            public int decimalPlaces { get; set; }
            public bool isInError { get; set; }
            public bool isEnabled { get; set; }
            public bool isFailPayload { get; set; }
            public int? measurementUnityId { get; set; }
            public string measurementUnity { get; set; }
        }

        public class InstrumentFunctionsResponse
        {
            public int resultsQty { get; set; }
            public List<InstrumentFunction> results { get; set; }
            public int status { get; set; }
        }

        public class InstrumentFunction
        {
            public string code { get; set; }
            public string valueCode { get; set; }
            public bool enabled { get; set; }
            public double value { get; set; }
            public int valueType { get; set; }
            public string description { get; set; }
            public string extraDescription { get; set; }
            public double minValue { get; set; }
            public double maxValue { get; set; }
            public double defaultValue { get; set; }
            public int decimalPlaces { get; set; }
            public int? measurementUnityId { get; set; }
            public string measurementUnity { get; set; }
        }

        public class GasSensorResponse
        {
            public int code { get; set; }
            public string msg { get; set; }
            public List<GasSensorData> data { get; set; }
            public bool ok { get; set; }
        }

        public class GasSensorData
        {
            public string id { get; set; }
            public string imei { get; set; }
            public string imeiAlias { get; set; }
            public string deptName { get; set; }
            public string deptId { get; set; }
            public int deviceState { get; set; }
            public string deviceStateName { get; set; }
            public int useState { get; set; }
            public int bindState { get; set; }
            public int stocks { get; set; }
            public string deviceTypeId { get; set; }
            public string deviceTypeName { get; set; }
            public string deviceModelId { get; set; }
            public string deviceModelName { get; set; }
            public int online { get; set; }
            public int type { get; set; }
            public SensorTransmissionData sensorTransmissionData { get; set; }
        }

        public class SensorTransmissionData
        {
            public List<SensorTransmissionDataDetail> sensorTransmissionDataDetailsList { get; set; }
        }

        public class SensorTransmissionDataDetail
        {
            public string sensorType { get; set; }
            public string sensorTypeName { get; set; }
            public string sensorUnit { get; set; }
            public string sensorUnitName { get; set; }
            public string sensorVal { get; set; }
            public string zName { get; set; }
            public string detectionTypeName { get; set; }
        }

        #endregion

        #region Sistema de Reintentos para Setpoints

        private async Task<bool> VerifySetpointApplied(int instrumentId, string code, double expectedValue)
        {
            try
            {
                var apiBaseUrl = Environment.GetEnvironmentVariable("SITRAD_API_URL")
                                 ?? "https://200.40.68.62:20108/api/v1";
                var fnUrl = $"{apiBaseUrl}/instruments/{instrumentId}/functions";
                var fnResp = await _httpClient.GetAsync(fnUrl);
                
                if (fnResp.IsSuccessStatusCode)
                {
                    var fc = JsonConvert.DeserializeObject<InstrumentFunctionsResponse>(
                                 await fnResp.Content.ReadAsStringAsync());
                    var setpoint = fc.results.FirstOrDefault(f => 
                        f.code.Equals(code, StringComparison.OrdinalIgnoreCase));
                    
                    if (setpoint != null)
                    {
                        // Verificar si el valor coincide (con tolerancia de 0.1°C)
                        return Math.Abs(setpoint.value - expectedValue) <= 0.1;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void AddToRetryQueue(int instrumentId, string code, double value)
        {
            var key = $"{instrumentId}_{code}";
            lock (_retryLock)
            {
                _retryQueue[key] = new SetpointRetryInfo
                {
                    InstrumentId = instrumentId,
                    Code = code,
                    Value = value,
                    Attempts = 0,
                    LastAttempt = DateTime.Now,
                    NextRetry = DateTime.Now.AddSeconds(10) // Primer reintento en 10 segundos
                };
            }
        }

        // Método para procesar reintentos (se ejecuta en background)
        public static async Task ProcessRetryQueue()
        {
            while (true)
            {
                try
                {
                    var retriesToProcess = new List<SetpointRetryInfo>();
                    
                    lock (_retryLock)
                    {
                        var now = DateTime.Now;
                        retriesToProcess = _retryQueue.Values
                            .Where(r => r.NextRetry <= now && r.Attempts < 10) // Máximo 10 intentos
                            .ToList();
                    }

                    foreach (var retry in retriesToProcess)
                    {
                        await ProcessRetry(retry);
                    }

                    // Limpiar reintentos exitosos o que excedieron el límite
                    lock (_retryLock)
                    {
                        var toRemove = _retryQueue.Where(kvp => 
                            kvp.Value.Attempts >= 10 || 
                            kvp.Value.NextRetry > DateTime.Now.AddMinutes(5)).ToList();
                        
                        foreach (var item in toRemove)
                        {
                            _retryQueue.Remove(item.Key);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error pero continuar
                    Console.WriteLine($"Error en procesamiento de reintentos: {ex.Message}");
                }

                await Task.Delay(5000); // Revisar cada 5 segundos
            }
        }

        private static string GetApiCredentials()
        {
            var username = Environment.GetEnvironmentVariable("SITRAD_API_USERNAME") 
                          ?? "CHANGE_THIS_USERNAME";
            var password = Environment.GetEnvironmentVariable("SITRAD_API_PASSWORD") 
                          ?? "CHANGE_THIS_PASSWORD";
            return Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
        }

        private static async Task ProcessRetry(SetpointRetryInfo retry)
        {
            try
            {
                var apiBaseUrl = Environment.GetEnvironmentVariable("SITRAD_API_URL")
                                 ?? "https://200.40.68.62:20108/api/v1";
                var postUrl = $"{apiBaseUrl}/instruments/{retry.InstrumentId}/functions";

                var payload = new
                {
                    instrumentId = retry.InstrumentId.ToString(),
                    code = retry.Code,
                    value = retry.Value,
                    showSpc = true,
                    sandbox = false
                };

                using var httpClient = new HttpClient();
                var credentials = GetApiCredentials();
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", credentials);

                var response = await httpClient.PostAsJsonAsync(postUrl, payload);
                
                lock (_retryLock)
                {
                    var key = $"{retry.InstrumentId}_{retry.Code}";
                    if (_retryQueue.ContainsKey(key))
                    {
                        _retryQueue[key].Attempts++;
                        _retryQueue[key].LastAttempt = DateTime.Now;
                        
                        if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
                        {
                            // Verificar si se aplicó correctamente
                            var verifyTask = VerifySetpointAppliedAsync(retry.InstrumentId, retry.Code, retry.Value);
                            if (verifyTask.Result)
                            {
                                _retryQueue.Remove(key); // Éxito, remover de la cola
                            }
                            else
                            {
                                // Programar siguiente reintento
                                _retryQueue[key].NextRetry = DateTime.Now.AddSeconds(15);
                            }
                        }
                        else
                        {
                            // Programar siguiente reintento con intervalo exponencial
                            var delay = Math.Min(60, 10 * Math.Pow(2, _retryQueue[key].Attempts));
                            _retryQueue[key].NextRetry = DateTime.Now.AddSeconds(delay);
                        }
                    }
                }
            }
            catch
            {
                // En caso de error, programar reintento
                lock (_retryLock)
                {
                    var key = $"{retry.InstrumentId}_{retry.Code}";
                    if (_retryQueue.ContainsKey(key))
                    {
                        _retryQueue[key].Attempts++;
                        _retryQueue[key].NextRetry = DateTime.Now.AddSeconds(30);
                    }
                }
            }
        }

        private static async Task<bool> VerifySetpointAppliedAsync(int instrumentId, string code, double expectedValue)
        {
            try
            {
                var apiBaseUrl = Environment.GetEnvironmentVariable("SITRAD_API_URL")
                                 ?? "https://200.40.68.62:20108/api/v1";
                var fnUrl = $"{apiBaseUrl}/instruments/{instrumentId}/functions";
                
                using var httpClient = new HttpClient();
                var credentials = GetApiCredentials();
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", credentials);
                
                var fnResp = await httpClient.GetAsync(fnUrl);
                
                if (fnResp.IsSuccessStatusCode)
                {
                    var fc = JsonConvert.DeserializeObject<InstrumentFunctionsResponse>(
                                 await fnResp.Content.ReadAsStringAsync());
                    var setpoint = fc.results.FirstOrDefault(f => 
                        f.code.Equals(code, StringComparison.OrdinalIgnoreCase));
                    
                    if (setpoint != null)
                    {
                        return Math.Abs(setpoint.value - expectedValue) <= 0.1;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public class SetpointRetryInfo
        {
            public int InstrumentId { get; set; }
            public string Code { get; set; }
            public double Value { get; set; }
            public int Attempts { get; set; }
            public DateTime LastAttempt { get; set; }
            public DateTime NextRetry { get; set; }
        }

        #endregion
    }
}
