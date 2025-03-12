using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class UDPClient : MonoBehaviour
{
    private UdpClient udpClient; // Cliente UDP
    private IPEndPoint remoteEndPoint; // Endpoint del servidor
    public bool isServerConnected = false; // Indicador de conexión

    // Cola para ejecutar acciones en el hilo principal (ya usada en recepción)
    private readonly System.Collections.Generic.Queue<Action> mainThreadActions = new System.Collections.Generic.Queue<Action>();

    // Inicia el cliente UDP con la IP y puerto del servidor
    public void StartUDPClient(string ipAddress, int port)
    {
        udpClient = new UdpClient();
        remoteEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
        udpClient.BeginReceive(ReceiveData, null);
        SendData("Hello, server!"); // Mensaje inicial
        isServerConnected = true;
    }

    // Método asíncrono para recibir datos
    private void ReceiveData(IAsyncResult result)
    {
        try
        {
            byte[] receivedBytes = udpClient.EndReceive(result, ref remoteEndPoint);

            // Encolar la acción para ejecutarla en el hilo principal
            lock (mainThreadActions)
            {
                mainThreadActions.Enqueue(() =>
                {
                    // Si no es un paquete de imagen (por ejemplo, mensaje de texto)
                    // Intentamos interpretarlo como imagen
                    Texture2D texture = new Texture2D(2, 2);
                    if (texture.LoadImage(receivedBytes))
                    {
                        Debug.Log("Recibida imagen del servidor, tamaño: " + receivedBytes.Length + " bytes");
                        // Aquí podrías asignarla a algún componente UI si lo deseas
                    }
                    else
                    {
                        string receivedMessage = System.Text.Encoding.UTF8.GetString(receivedBytes);
                        Debug.Log("Recibido del servidor: " + receivedMessage);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.Log("Error al recibir datos: " + ex.Message);
        }
        // Continuar recibiendo datos
        udpClient.BeginReceive(ReceiveData, null);
    }

    // Ejecuta las acciones encoladas en Update (en el hilo principal)
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

    // Enviar un mensaje de texto al servidor
    public void SendData(string message)
    {
        byte[] sendBytes = System.Text.Encoding.UTF8.GetBytes(message);
        udpClient.Send(sendBytes, sendBytes.Length, remoteEndPoint);
        Debug.Log("Enviado al servidor: " + message);
    }

    // Enviar una imagen al servidor (fragmentada)
    public void SendImage(Texture2D image)
    {
        if (image == null)
        {
            Debug.Log("No hay imagen para enviar.");
            return;
        }
        // Convertir la imagen a array de bytes en formato PNG
        byte[] imageData = image.EncodeToPNG();

        const int headerSize = 16; // 4 bytes magic + 4 bytes fragment index + 4 bytes total fragments + 4 bytes imageId
        const int maxPacketSize = 1024; // Tamaño máximo de cada paquete (ajusta según necesidad)
        int fragmentSize = maxPacketSize - headerSize;
        int totalFragments = Mathf.CeilToInt(imageData.Length / (float)fragmentSize);
        // Generar un identificador único para esta imagen (puede ser un contador o un valor aleatorio)
        int imageId = UnityEngine.Random.Range(0, int.MaxValue);

        Debug.Log("Enviando imagen id: " + imageId + ", en " + totalFragments + " fragmentos.");
        for (int fragmentIndex = 0; fragmentIndex < totalFragments; fragmentIndex++)
        {
            int offset = fragmentIndex * fragmentSize;
            int currentFragmentSize = Mathf.Min(fragmentSize, imageData.Length - offset);
            byte[] packet = new byte[headerSize + currentFragmentSize];
            // Encabezado: primeros 4 bytes: magic "IMGF"
            byte[] magic = System.Text.Encoding.ASCII.GetBytes("IMGF");
            Array.Copy(magic, 0, packet, 0, 4);
            // Siguientes 4 bytes: índice del fragmento
            Array.Copy(BitConverter.GetBytes(fragmentIndex), 0, packet, 4, 4);
            // Siguientes 4 bytes: total de fragmentos
            Array.Copy(BitConverter.GetBytes(totalFragments), 0, packet, 8, 4);
            // Siguientes 4 bytes: imageId
            Array.Copy(BitConverter.GetBytes(imageId), 0, packet, 12, 4);
            // Copiar el fragmento de datos
            Array.Copy(imageData, offset, packet, headerSize, currentFragmentSize);
            // Enviar el paquete
            udpClient.Send(packet, packet.Length, remoteEndPoint);
        }
        Debug.Log("Imagen enviada en " + totalFragments + " fragmentos.");
    }
}
