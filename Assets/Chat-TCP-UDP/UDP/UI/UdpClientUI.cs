using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UdpClientUI : MonoBehaviour
{
    public int serverPort = 5555;
    public string serverAddress = "127.0.0.1";
    [SerializeField] private UDPClient _client;
    [SerializeField] private TMP_InputField messageInput;

    // Campo para asignar la imagen a enviar desde la UI (puede ser asignada en el inspector)
    public Texture2D imageToSend;

    // Conectar el cliente al servidor
    public void ConnectClient()
    {
        _client.StartUDPClient(serverAddress, serverPort);
    }

    // Enviar un mensaje de texto desde la UI
    public void SendClientMessage()
    {
        if(!_client.isServerConnected)
        {
            Debug.Log("El cliente no está conectado");
            return;
        }

        if(string.IsNullOrEmpty(messageInput.text))
        {
            Debug.Log("El mensaje está vacío");
            return;
        }

        string message = messageInput.text;
        _client.SendData(message);
    }

    // Enviar una imagen desde la UI
    public void SendClientImage()
    {
        if(!_client.isServerConnected)
        {
            Debug.Log("El cliente no está conectado");
            return;
        }

        if(imageToSend == null)
        {
            Debug.Log("No hay imagen asignada para enviar");
            return;
        }

        _client.SendImage(imageToSend);
    }
}
