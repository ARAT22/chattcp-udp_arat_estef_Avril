using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class UDPServer : MonoBehaviour
{
    private UdpClient udpServer; // Servidor UDP
    private IPEndPoint remoteEndPoint; // Endpoint del cliente
    public bool isServerRunning = false; // Indicador de servidor en ejecución

    // Componente UI para mostrar la imagen recibida (debe asignarse en el Inspector)
    public RawImage receivedImageDisplay;

    // Cola para encolar acciones que se ejecutarán en el hilo principal
    private readonly Queue<Action> mainThreadActions = new Queue<Action>();

    // Diccionario para almacenar los fragmentos de imagen en proceso de reensamblado
    private Dictionary<int, ImageAssembly> imageAssemblies = new Dictionary<int, ImageAssembly>();

    // Clase interna para almacenar los fragmentos de una imagen
    private class ImageAssembly
    {
        public int totalFragments;
        public Dictionary<int, byte[]> fragments = new Dictionary<int, byte[]>();
    }

    // Inicia el servidor UDP en el puerto indicado
    public void StartUDPServer(int port)
    {
        try
        {
            if (udpServer != null)
            {
                udpServer.Close();
                udpServer = null;
            }
            udpServer = new UdpClient();
            udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpServer.Client.Bind(new IPEndPoint(IPAddress.Any, port));

            // Para pruebas, asignamos manualmente el endpoint del cliente.
            // En un escenario real, este valor se actualizará al recibir el primer mensaje del cliente.
            remoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);

            Debug.Log("Servidor iniciado en puerto " + port + ". Esperando mensajes...");
            udpServer.BeginReceive(ReceiveData, null);
            isServerRunning = true;
        }
        catch (Exception ex)
        {
            Debug.Log("Error al iniciar el servidor: " + ex.Message);
        }
    }

    // Método asíncrono para recibir datos
    private void ReceiveData(IAsyncResult result)
    {
        try
        {
            byte[] receivedBytes = udpServer.EndReceive(result, ref remoteEndPoint);

            lock (mainThreadActions)
            {
                mainThreadActions.Enqueue(() =>
                {
                    // Si el paquete tiene al menos 16 bytes, intentar tratarlo como fragmento de imagen
                    if (receivedBytes.Length >= 16)
                    {
                        string magic = System.Text.Encoding.ASCII.GetString(receivedBytes, 0, 4);
                        if (magic == "IMGF")
                        {
                            int fragmentIndex = BitConverter.ToInt32(receivedBytes, 4);
                            int totalFragments = BitConverter.ToInt32(receivedBytes, 8);
                            int imageId = BitConverter.ToInt32(receivedBytes, 12);
                            int headerSize = 16;
                            int payloadSize = receivedBytes.Length - headerSize;
                            byte[] fragmentData = new byte[payloadSize];
                            Array.Copy(receivedBytes, headerSize, fragmentData, 0, payloadSize);

                            Debug.Log($"[ImageID {imageId}] Recibido fragmento {fragmentIndex + 1}/{totalFragments}");

                            if (!imageAssemblies.ContainsKey(imageId))
                            {
                                imageAssemblies[imageId] = new ImageAssembly() { totalFragments = totalFragments };
                            }
                            imageAssemblies[imageId].fragments[fragmentIndex] = fragmentData;

                            if (imageAssemblies[imageId].fragments.Count == totalFragments)
                            {
                                int totalLength = 0;
                                for (int i = 0; i < totalFragments; i++)
                                {
                                    totalLength += imageAssemblies[imageId].fragments[i].Length;
                                }
                                byte[] fullImageData = new byte[totalLength];
                                int offset = 0;
                                for (int i = 0; i < totalFragments; i++)
                                {
                                    byte[] frag = imageAssemblies[imageId].fragments[i];
                                    Array.Copy(frag, 0, fullImageData, offset, frag.Length);
                                    offset += frag.Length;
                                }
                                imageAssemblies.Remove(imageId);

                                // Guardar la imagen reconstruida en disco para depuración
                                string filePath = Path.Combine(Application.dataPath, "ReconstructedImage.png");
                                File.WriteAllBytes(filePath, fullImageData);
                                Debug.Log("Imagen guardada en: " + filePath);

                                Texture2D texture = new Texture2D(2, 2);
                                if (texture.LoadImage(fullImageData))
                                {
                                    Debug.Log("Imagen reconstruida del cliente, tamaño: " + fullImageData.Length + " bytes");
                                    if (receivedImageDisplay != null)
                                    {
                                        receivedImageDisplay.texture = texture;
                                    }
                                    else
                                    {
                                        Debug.LogWarning("No se ha asignado un RawImage para mostrar la imagen.");
                                    }
                                }
                                else
                                {
                                    Debug.Log("Error al reconstruir la imagen del cliente.");
                                }
                            }
                            return; // Finaliza el procesamiento de este paquete
                        }
                    }
                    // Si no es un fragmento de imagen, tratarlo como mensaje de texto
                    string receivedMessage = System.Text.Encoding.UTF8.GetString(receivedBytes);
                    Debug.Log("Recibido del cliente: " + receivedMessage);
                });
            }
        }
        catch (Exception ex)
        {
            Debug.Log("Error al recibir datos: " + ex.Message);
        }
        udpServer.BeginReceive(ReceiveData, null);
    }

    // En el método Update se ejecutan las acciones encoladas en el hilo principal
    private void Update()
    {
        while (true)
        {
            Action action = null;
            lock (mainThreadActions)
            {
                if (mainThreadActions.Count > 0)
                {
                    action = mainThreadActions.Dequeue();
                }
            }
            if (action == null)
                break;
            action();
        }
    }

    // Enviar un mensaje de texto al cliente
    public void SendData(string message)
    {
        if (remoteEndPoint == null || remoteEndPoint.Address.Equals(IPAddress.Any))
        {
            Debug.Log("No hay un cliente conectado para enviar el mensaje.");
            return;
        }
        byte[] sendBytes = System.Text.Encoding.UTF8.GetBytes(message);
        udpServer.Send(sendBytes, sendBytes.Length, remoteEndPoint);
        Debug.Log("Enviado al cliente: " + message);
    }
}
