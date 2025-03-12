using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class TCPServer : MonoBehaviour
{
    private TcpListener tcpListener;         // Servidor TCP
    private TcpClient connectedClient;         // Cliente conectado
    private NetworkStream networkStream;       // Flujo de datos
    private byte[] receiveBuffer;              // Buffer para datos recibidos

    public bool isServerRunning;

    // Cola para ejecutar acciones en el hilo principal
    private Queue<Action> _mainThreadQueue = new Queue<Action>();

    public void StartServer(int port)
    {
        try
        {
            tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();
            Debug.Log("Servidor iniciado, esperando conexiones...");
            tcpListener.BeginAcceptTcpClient(HandleIncomingConnection, null);
            isServerRunning = true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Error al iniciar el servidor: " + ex.Message);
        }
    }

    private void HandleIncomingConnection(IAsyncResult result)
    {
        try
        {
            connectedClient = tcpListener.EndAcceptTcpClient(result);
            networkStream = connectedClient.GetStream();
            Debug.Log("Cliente conectado: " + connectedClient.Client.RemoteEndPoint);
            receiveBuffer = new byte[connectedClient.ReceiveBufferSize];
            networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null);
            // Permite aceptar otros clientes (si lo requieres)
            tcpListener.BeginAcceptTcpClient(HandleIncomingConnection, null);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error en HandleIncomingConnection: " + ex.Message);
        }
    }

    private void ReceiveData(IAsyncResult result)
    {
        try
        {
            int bytesRead = networkStream.EndRead(result);
            if (bytesRead <= 0)
            {
                Debug.Log("Cliente desconectado: " + connectedClient.Client.RemoteEndPoint);
                connectedClient.Close();
                return;
            }

            // Se requieren 8 bytes para el header
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
                    Debug.Log("Mensaje recibido del cliente: " + receivedMessage);
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

                    // Encolar la acción para procesar la imagen en el hilo principal
                    lock (_mainThreadQueue)
                    {
                        _mainThreadQueue.Enqueue(() =>
                        {
                            Texture2D receivedTexture = new Texture2D(2, 2);
                            bool loaded = receivedTexture.LoadImage(imageBytes);
                            if (loaded)
                            {
                                Debug.Log("Imagen recibida del cliente (procesada en el hilo principal).");
                                // Reenvía la imagen al cliente para que se muestre en su UI
                                SendImage(receivedTexture);
                            }
                            else
                            {
                                Debug.LogWarning("Error al cargar la imagen recibida en el servidor (main thread).");
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
            Debug.LogError("Error en ReceiveData del servidor: " + ex.Message);
        }
    }

    // Este método se ejecuta en el hilo principal
    private void Update()
    {
        lock (_mainThreadQueue)
        {
            while (_mainThreadQueue.Count > 0)
            {
                _mainThreadQueue.Dequeue().Invoke();
            }
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
            Debug.Log("Mensaje enviado al cliente: " + message);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error al enviar mensaje desde el servidor: " + ex.Message);
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
            Debug.Log("Imagen reenviada al cliente.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error al reenviar imagen desde el servidor: " + ex.Message);
        }
    }
}
