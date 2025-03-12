using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using UnityEngine;

public class TCPClient : MonoBehaviour
{
    private TcpClient tcpClient;               // Cliente TCP para conectarse al servidor
    private NetworkStream networkStream;       // Flujo de datos para enviar/recibir
    private byte[] receiveBuffer;              // Buffer para almacenar los datos recibidos

    public bool isServerConnected;
    public event Action<Texture2D> OnImageReceived;  // Evento para notificar la recepción de imagen

    // Cola para encolar acciones que se ejecutarán en el hilo principal
    private Queue<Action> mainThreadQueue = new Queue<Action>();

    public void ConnectToServer(string ipAddress, int port)
    {
        try
        {
            tcpClient = new TcpClient();
            tcpClient.Connect(IPAddress.Parse(ipAddress), port);
            networkStream = tcpClient.GetStream();
            receiveBuffer = new byte[tcpClient.ReceiveBufferSize];
            networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null);
            isServerConnected = true;
            Debug.Log("Cliente conectado al servidor.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error al conectar al servidor: " + ex.Message);
        }
    }

    public void SendData(string message)
    {
        try
        {
            byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes("TXT_");
            byte[] lengthBytes = BitConverter.GetBytes(messageBytes.Length);

            byte[] packet = new byte[typeBytes.Length + lengthBytes.Length + messageBytes.Length];
            Buffer.BlockCopy(typeBytes, 0, packet, 0, typeBytes.Length);
            Buffer.BlockCopy(lengthBytes, 0, packet, typeBytes.Length, lengthBytes.Length);
            Buffer.BlockCopy(messageBytes, 0, packet, typeBytes.Length + lengthBytes.Length, messageBytes.Length);

            networkStream.Write(packet, 0, packet.Length);
            networkStream.Flush();
            Debug.Log("Mensaje enviado al servidor: " + message);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error al enviar mensaje: " + ex.Message);
        }
    }

    public void SendImage(Texture2D image)
    {
        try
        {
            byte[] imageBytes = image.EncodeToPNG();
            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes("IMG_");
            byte[] lengthBytes = BitConverter.GetBytes(imageBytes.Length);

            byte[] packet = new byte[typeBytes.Length + lengthBytes.Length + imageBytes.Length];
            Buffer.BlockCopy(typeBytes, 0, packet, 0, typeBytes.Length);
            Buffer.BlockCopy(lengthBytes, 0, packet, typeBytes.Length, lengthBytes.Length);
            Buffer.BlockCopy(imageBytes, 0, packet, typeBytes.Length + lengthBytes.Length, imageBytes.Length);

            networkStream.Write(packet, 0, packet.Length);
            networkStream.Flush();
            Debug.Log("Imagen enviada al servidor.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error al enviar imagen: " + ex.Message);
        }
    }

    private void ReceiveData(IAsyncResult result)
    {
        try
        {
            int bytesRead = networkStream.EndRead(result);
            if (bytesRead <= 0)
            {
                Debug.Log("Servidor desconectado.");
                tcpClient.Close();
                return;
            }

            // Se requieren 8 bytes para el header (4 para el tipo y 4 para la longitud)
            if (bytesRead < 8)
            {
                Debug.LogWarning("Datos insuficientes para header.");
                networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null);
                return;
            }

            string header = System.Text.Encoding.ASCII.GetString(receiveBuffer, 0, 4);
            int payloadLength = BitConverter.ToInt32(receiveBuffer, 4);

            if (header == "TXT_")
            {
                if (bytesRead >= 8 + payloadLength)
                {
                    string receivedMessage = System.Text.Encoding.UTF8.GetString(receiveBuffer, 8, payloadLength);
                    Debug.Log("Mensaje recibido del servidor: " + receivedMessage);
                }
                else
                {
                    Debug.LogWarning("Mensaje de texto incompleto.");
                }
            }
            else if (header == "IMG_")
            {
                if (bytesRead >= 8 + payloadLength)
                {
                    byte[] imageBytes = new byte[payloadLength];
                    Buffer.BlockCopy(receiveBuffer, 8, imageBytes, 0, payloadLength);

                    // Encola la acción para procesar la imagen en el hilo principal
                    lock (mainThreadQueue)
                    {
                        mainThreadQueue.Enqueue(() =>
                        {
                            Texture2D receivedTexture = new Texture2D(2, 2);
                            bool loaded = receivedTexture.LoadImage(imageBytes);
                            if (loaded)
                            {
                                Debug.Log("Imagen recibida del servidor (procesada en el hilo principal).");
                                OnImageReceived?.Invoke(receivedTexture);
                            }
                            else
                            {
                                Debug.LogWarning("Error al cargar la imagen recibida.");
                            }
                        });
                    }
                }
                else
                {
                    Debug.LogWarning("Imagen incompleta.");
                }
            }
            else
            {
                Debug.LogWarning("Tipo de mensaje desconocido: " + header);
            }

            // Reinicia la lectura de datos de forma asíncrona
            networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error en ReceiveData: " + ex.Message);
        }
    }

    // Se procesa la cola de acciones en el hilo principal
    private void Update()
    {
        lock (mainThreadQueue)
        {
            while (mainThreadQueue.Count > 0)
            {
                mainThreadQueue.Dequeue().Invoke();
            }
        }
    }
}

