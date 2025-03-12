using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TCPClientUI : MonoBehaviour
{
    public int serverPort = 5555;
    public string serverAddress = "127.0.0.1";
    [SerializeField] private TCPClient _client;
    [SerializeField] private TMP_InputField messageInput;
    
    // Imagen que se enviará
    [SerializeField] private Texture2D imageToSend;

    // Componente UI para mostrar la imagen recibida
    [SerializeField] private RawImage receivedImage;

    void Start()
    {
        // Asegúrate de que el componente RawImage esté asignado en el Inspector
        if (receivedImage == null)
            Debug.LogWarning("RawImage no asignado en TCPClientUI.");

        // Suscribe el método para actualizar la UI cuando se reciba una imagen
        _client.OnImageReceived += DisplayReceivedImage;
    }

    public void ConnectClient()
    {
        _client.ConnectToServer(serverAddress, serverPort);
    }

    public void SendClientMessage()
    {
        if (!_client.isServerConnected)
        {
            Debug.Log("El cliente no está conectado");
            return;
        }

        if (string.IsNullOrEmpty(messageInput.text))
        {
            Debug.Log("El mensaje de chat está vacío");
            return;
        }

        string message = messageInput.text;
        _client.SendData(message);
    }

    public void SendClientImage()
    {
        if (!_client.isServerConnected)
        {
            Debug.Log("El cliente no está conectado");
            return;
        }

        if (imageToSend == null)
        {
            Debug.Log("No hay imagen para enviar");
            return;
        }

        _client.SendImage(imageToSend);
    }

    /// <summary>
    /// Actualiza el RawImage de la UI con la textura recibida.
    /// </summary>
    /// <param name="texture">La textura recibida del servidor.</param>
    private void DisplayReceivedImage(Texture2D texture)
    {
        if (receivedImage != null)
        {
            receivedImage.texture = texture;
            Debug.Log("Imagen mostrada en la UI.");
        }
        else
        {
            Debug.LogWarning("No se ha asignado el RawImage en la UI.");
        }
    }
}
