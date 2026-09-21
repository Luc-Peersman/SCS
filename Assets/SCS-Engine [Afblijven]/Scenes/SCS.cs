using UnityEngine;
using System.IO.Ports;
public class Esp32Serial : MonoBehaviour
{
    // Verander dit naar jouw COM-poort (bijv. "COM3" of "/dev/cu.usbserial-...")
    public string portName = "COM6";
    public int baudRate = 115200;
    private SerialPort sp;
    bool sent = false;
    void Start()
    {
        Debug.Log("Start Esp32Serial Called");
        sp = new SerialPort(portName, baudRate);
        sp.ReadTimeout = 50; // Voorkomt dat Unity vastloopt bij wachten op data
        try
        {
            sp.Open();
            Debug.Log("Verbinding met ESP32 succesvol!");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Kan poort niet openen: " + e.Message);
        }
    }
    void Update()
    {

        if (sp != null && sp.IsOpen)
        {
            Debug.Log("sp != null && sp.IsOpen");
            try
            {
                string dataIn = sp.ReadLine();
                if (dataIn != null)
                {
                    Debug.Log("Ontvangen: " + dataIn);
                    // Verwerk je data hier (bijv. positie veranderen, UI updaten)
                }
            }
            catch (System.TimeoutException)
            {
                Debug.Log("Geen data ontvangen");
                // Geen data ontvangen binnen de timeout, ga door
            }
        }
        if (!sent)
        {
            sp.WriteLine("FF PING");
            sent = true;
            Debug.Log("Command FF PING via WriteLine verzonden");

        }
    }
    void OnDestroy()
    {
        if (sp != null && sp.IsOpen)
        {
            sp.Close();
        }
    }
}