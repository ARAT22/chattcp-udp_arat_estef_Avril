using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UdpServerUI : MonoBehaviour
{
    public int serverPort = 5555;
    [SerializeField] private UDPServer _server;
    [SerializeField] private TMP_InputField messageInput;

    // Iniciar el servidor desde la UI
    public void StartServer()
    {
        _server.StartUDPServer(serverPort);
    }

    // Enviar un mensaje de texto al cliente desde la UI
    public void SendServerMessage()
    {
        if(!_server.isServerRunning)
        {
            Debug.Log("El servidor no está en ejecución");
            return;
        }

        if(string.IsNullOrEmpty(messageInput.text))
        {
            Debug.Log("El mensaje está vacío");
            return;
        }

        string message = messageInput.text;
        _server.SendData(message);
    }
}
