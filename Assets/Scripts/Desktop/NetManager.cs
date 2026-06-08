using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class NetManager
{
    #region Singleton
    private volatile static NetManager m_instance;
    private static object m_locker = new object();

    public static NetManager M_Instance
    {
        get
        {
            lock (m_locker)
            {
                if (m_instance == null)
                {
                    m_instance = new NetManager();
                }
            }
            return m_instance;
        }
    }
    #endregion

    private NetManager() { }

    private ClientWebSocket m_clientWebSocket;
    private Thread m_dataReceiveThread;
    private bool m_isDoThread;

    public bool GetNetStatus() 
    {
        return m_isDoThread;
    }

    public Queue<string> response_queue = new Queue<string>();

    public void Connect(string uriStr)
    {
        try
        {
            m_clientWebSocket = new ClientWebSocket();

            m_isDoThread = true;

            m_dataReceiveThread = new Thread(ReceiveData);
            m_dataReceiveThread.IsBackground = true;

            var task = m_clientWebSocket.ConnectAsync(new Uri(uriStr), CancellationToken.None);
            task.Wait();

            m_dataReceiveThread.Start(m_clientWebSocket);

            if (m_clientWebSocket.State == WebSocketState.Open)
            {
                Debug.Log("Connected to server.");
            }
        }
        catch (Exception ex)
        {
            m_isDoThread = false;
            Debug.LogError("Connect error: " + ex.Message);
            Debug.LogError("WebSocket state: " + m_clientWebSocket.State);
            CloseClientWebSocket();
        }

    }

    private void ReceiveData(object socket)
    {
        ClientWebSocket socketClient = (ClientWebSocket)socket;
        while (m_isDoThread)
        {
            string data = Receive(socketClient);
            if (data != null)
            {
                Debug.Log("Received server message: " + data);
                response_queue.Enqueue(data);
                if (response_queue.Count > 5)
                { 
                    response_queue.Dequeue();
                }
            }
        }
        Debug.Log("Receive thread ended.");
    }

    private string Receive(ClientWebSocket socket)
    {
        try
        {
            if (socket != null && (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseSent))
            {
                byte[] arrry = new byte[3072];
                ArraySegment<byte> buffer = new ArraySegment<byte>(arrry);
                var task = socket.ReceiveAsync(buffer, CancellationToken.None);
                task.Wait();

                Debug.Log("Socket current state: " + socket.State);

                if (socket.State == WebSocketState.CloseReceived || task.Result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }
                return Encoding.UTF8.GetString(buffer.Array, 0, task.Result.Count);
            }
            else
            {
                return null;
            }
        }
        catch (WebSocketException ex)
        {
            Debug.LogError("Receive server message error: " + ex.Message);
            CloseClientWebSocket();
            return null;
        }
    }

    public void Send(string content)
    {
        try
        {
            if (m_clientWebSocket != null && (m_clientWebSocket.State == WebSocketState.Open || m_clientWebSocket.State == WebSocketState.CloseReceived))
            {
                ArraySegment<byte> array = new ArraySegment<byte>(Encoding.UTF8.GetBytes(content));
                var task = m_clientWebSocket.SendAsync(array, WebSocketMessageType.Binary, true, CancellationToken.None);
                task.Wait();

                Debug.Log("Message sent.");
            }
        }
        catch (WebSocketException ex)
        {
            Debug.LogError("Send message error: " + ex.Message);
            CloseClientWebSocket();
        }
    }

    public void CloseClientWebSocket()
    {
        if (m_clientWebSocket != null && m_clientWebSocket.State == WebSocketState.Open)
        {
            var task = m_clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
            Debug.Log("Closing connection, current socket state: " + m_clientWebSocket.State);
            task.Wait();
            Debug.Log("Socket state after close: " + m_clientWebSocket.State);
            Debug.Log("Connection closed.");
        }
        if (m_dataReceiveThread != null && m_dataReceiveThread.IsAlive)
        {
            m_isDoThread = false;
            m_dataReceiveThread = null;
        }
    }
}
