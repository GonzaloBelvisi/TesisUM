namespace SitradWebInterface.Models
{
    public class CameraViewModel
{
    public int InstrumentId { get; set; }
    public string CameraName { get; set; }
    public string Temperature { get; set; }    // sigue siendo la “Temperatura” genérica/humedad
    public string Humidity    { get; set; }

    // **NUEVO**: temperatura de pulpa
    public string PulpTemp    { get; set; }

    // **RENOMBRADO**: antes PulpTemperature, ahora EvaporatorTemp
    public string EvaporatorTemp { get; set; }

    public string Set1 { get; set; }
    public string Set3 { get; set; }
    public string co2  { get; set; }
    public string etileno { get; set; }
}

}