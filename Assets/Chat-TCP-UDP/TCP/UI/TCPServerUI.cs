using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TCPServerUI : MonoBehaviour
{
    public int serverPort = 5555;
    [SerializeField] private TCPServer _server;
    [SerializeField] private TMP_InputField messageInput;
    
    // Imagen que se enviará (si lo deseas)
    [SerializeField] private Texture2D imageToSend;

    public void StartServer()
    {
        _server.StartServer(serverPort);
    }

    public void SendServerMessage()
    {
        if (!_server.isServerRunning)
        {
            Debug.Log("El servidor no está corriendo");
            return;
        }

        if (string.IsNullOrEmpty(messageInput.text))
        {
            Debug.Log("El mensaje de chat está vacío");
            return;
        }

        string message = messageInput.text;
        _server.SendData(message);
    }

    public void SendServerImage()
    {
        if (!_server.isServerRunning)
        {
            Debug.Log("El servidor no está corriendo");
            return;
        }

        if (imageToSend == null)
        {
            Debug.Log("No hay imagen para enviar");
            return;
        }

        _server.SendImage(imageToSend);
    }
}
