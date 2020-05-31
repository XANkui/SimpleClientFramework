using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class NetManager : Singleton<NetManager>
{
    public enum NetEvent {
        ConnectSuccess=1,
        ConnectFail =2,
        Close =3,
    }

    public string PublicKey = "TestPublicKey";

    public string Secretkey { get; private set; }

    private Socket m_Socket;

    private string m_IP;
    private int m_Port;

    private bool m_Contecting = false;
    private bool m_Closing = false;
    private ByteArray m_ReadBuff;

    // 消息List
    private List<MessageBase> m_MessageList;
    private List<MessageBase> m_UnityMessageList;
    private int m_MessageCount = 0;

    // 发送给服务器的消息队列
    private Queue<ByteArray> m_WriteQueue;

    //心跳包时间计时
    static long lastPingTime;
    static long lastPongTime;

    //心跳间隔时间
    private static long m_PingInterval = 30;

    // 消息处理线程
    private Thread m_MessageThread;
    private Thread m_HeartThread;


    // 重新连接部分
    // 是否掉线
    private bool m_Offline = false;
    // 是否第一次连接成功过（是否登陆过）
    private bool m_IsFirstConnectSuccessed = false;
    // 是否是掉线重连
    private bool m_Reconnect = false;

    // 切换网络重新连接
    private NetworkReachability m_CurrentNetwork = NetworkReachability.NotReachable;
    public IEnumerator CheckNet() {
        m_CurrentNetwork = Application.internetReachability;
        while (true)
        {
            yield return new WaitForSeconds(1);

            if (m_IsFirstConnectSuccessed)
            {
                if (m_CurrentNetwork != Application.internetReachability)
                {
                    Reconnect();
                    m_CurrentNetwork = Application.internetReachability;
                }
                
            }
        }
    }


    // Socket 相关事件监听
    public delegate void EventListener(string str);
    private Dictionary<NetEvent, EventListener> m_ListenerDic = new Dictionary<NetEvent, EventListener>();

    // Protocol 相关事件处理监听
    public delegate void ProtocalListener(MessageBase messageBase);
    private Dictionary<ProtocolEnum, ProtocalListener> m_ProtocalListenerDic = new Dictionary<ProtocolEnum, ProtocalListener>();

    /// <summary>
    /// 添加事件监听
    /// </summary>
    /// <param name="netEvent"></param>
    /// <param name="eventListener"></param>
    public void AddEventListener(NetEvent netEvent, EventListener eventListener) {
        if (m_ListenerDic.ContainsKey(netEvent))
        {
            m_ListenerDic[netEvent] += eventListener;

        }
        else {
            m_ListenerDic[netEvent] = eventListener;

        }

    }

    /// <summary>
    /// 移除事件监听
    /// </summary>
    /// <param name="netEvent"></param>
    /// <param name="eventListener"></param>
    public void RemoveEventListener(NetEvent netEvent, EventListener eventListener)
    {
        if (m_ListenerDic.ContainsKey(netEvent))
        {
            m_ListenerDic[netEvent] -= eventListener;

            if (m_ListenerDic[netEvent]==null)
            {
                m_ListenerDic.Remove(netEvent);
            }

        }
        

    }

    /// <summary>
    /// 一个协议希望只有一个监听
    /// </summary>
    /// <param name="protocolEnum"></param>
    /// <param name="protocalListener"></param>
    public void AddProtocalListener(ProtocolEnum protocolEnum, ProtocalListener protocalListener) {
        m_ProtocalListenerDic[protocolEnum] = protocalListener;
    }

    /// <summary>
    /// 执行协议的监听事件
    /// </summary>
    /// <param name="protocolEnum"></param>
    /// <param name="messageBase"></param>
    public void FirstProtocal(ProtocolEnum protocolEnum, MessageBase messageBase) {
        if (m_ProtocalListenerDic.ContainsKey(protocolEnum))
        {
            m_ProtocalListenerDic[protocolEnum](messageBase);
        }
    }

    /// <summary>
    /// 执行监听事件
    /// </summary>
    /// <param name="netEvent"></param>
    /// <param name="str"></param>
    void FirstEvent(NetEvent netEvent, string str) {
        if (m_ListenerDic.ContainsKey(netEvent)) {
            m_ListenerDic[netEvent](str);
        }
    }

    public void Update() {

        if (m_Offline == true && m_IsFirstConnectSuccessed)
        {

            // 弹出窗口，是否重新连接

            Reconnect();


            m_Offline = false;

        }

        // 断开后重新连接服务器
        if (string.IsNullOrEmpty(Secretkey)==false && m_Socket.Connected ==true && m_Reconnect == true)
        {
            // 本地保存了我们的账户和Token,然后进行判断有误账户和Token

            //使用Token 登录
            //ProtocalManager.Login(LoginType.Token,"username","token",(res,token)=> {
            //    switch (res)
            //    {
            //        case LoginResult.Success:
            //            break;
            //        case LoginResult.Fail:
            //            break;
            //        case LoginResult.WrongPwd:
            //            break;
            //        case LoginResult.UserNotExit:
            //            break;
            //        case LoginResult.TimeoutToken:
            //            break;
            //        default:
            //            break;
            //    }
            //});


            m_Reconnect = false;
        }


        MessageUpdate();


        
    }

    /// <summary>
    /// 在 unity 中处理的消息
    /// </summary>
    void MessageUpdate() {
        if (m_Socket != null && m_Socket.Connected)
        {
            if (m_MessageCount == 0)
            {
                return;

            }

            MessageBase messageBase = null;
            lock (m_UnityMessageList)
            {
                if (m_UnityMessageList.Count>0)
                {
                    messageBase = m_UnityMessageList[0];
                    m_UnityMessageList.RemoveAt(0);
                    m_MessageCount--;
                }
            }

            if (messageBase != null)
            {
                FirstProtocal(messageBase.ProtoType, messageBase);
            }
        }
    }


    /// <summary>
    /// 消息分发处理
    /// </summary>
    void MessageThread() {
        while (m_Socket != null && m_Socket.Connected)
        {
            if (m_MessageList.Count <= 0) 
            {
                continue;
            }

            MessageBase messageBase = null;
            lock (m_MessageList)
            {
                if (m_MessageList.Count>0)
                {
                    messageBase = m_MessageList[0];
                    m_MessageList.RemoveAt(0);
                }
            }

            if (messageBase != null)
            {
                // 心跳包处理
                if (messageBase is MessagePing)
                {
                    lastPongTime = GetTimeStamp();
                    Debug.Log("收到服务端心跳包！！！");

                    m_MessageCount--;
                }
                // 需要在 Unity 处理的线程消息 
                else
                {
                    lock (m_UnityMessageList)
                    {
                        m_UnityMessageList.Add(messageBase);
                    }
                }
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// 心跳线程，给服务器判断，是否维持与服务器的连接
    /// </summary>
    void PingThread() {
        while (m_Socket != null && m_Socket.Connected) {
            long timeNow = GetTimeStamp();
            if (timeNow - lastPingTime > m_PingInterval)
            {
                MessagePing messagePing = new MessagePing();
                SendMessage(messagePing);
                lastPingTime = GetTimeStamp();
            }

            // 如果心跳包过长时间没有收到，就关闭连接
            if (timeNow - lastPongTime>m_PingInterval * 4)
            {
                Close(false);
            }
        }
    }

    /// <summary>
    /// 重新连接网络
    /// </summary>
    void Reconnect() {
        Connect(m_IP,m_Port);

        // 重新连接
        m_Reconnect = true;
    }


    /// <summary>
    /// 连接服务器
    /// </summary>
    /// <param name="IP"></param>
    /// <param name="Port"></param>
    public void Connect(string IP, int Port) {
        if (m_Socket !=null && m_Socket.Connected) {
            Debug.LogError("连接失败，已经连接了");
            return;
        }

        if (m_Contecting ==true)
        {
            Debug.LogError("连接失败，正在连接中");
            return;
        }

        InitState();
        m_Socket.NoDelay = true;
        m_Contecting = true;
        m_Socket.BeginConnect(IP,Port,ConnectCallback, m_Socket);
        m_IP = IP;
        m_Port = Port;
    }
    
    /// <summary>
    /// 初始化
    /// </summary>
    void InitState() {
        // 初始化变量
        m_Socket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
        m_ReadBuff = new ByteArray();
        m_WriteQueue = new Queue<ByteArray>();
        m_Contecting = false;
        m_Closing = false;
        m_MessageList = new List<MessageBase>();
        m_UnityMessageList = new List<MessageBase>();
        m_MessageCount = 0;
        lastPingTime = GetTimeStamp();
        lastPongTime = GetTimeStamp();
    }

    /// <summary>
    /// 连接回调
    /// </summary>
    /// <param name="ar"></param>
    void ConnectCallback(IAsyncResult ar) {
        try
        {
            Socket socket = (Socket)ar.AsyncState;
            socket.EndConnect(ar);
            FirstEvent(NetEvent.ConnectSuccess, "连接服务器成功");
            Debug.Log("Socket Coneect Sucess");

            // 一次成功连接
            m_IsFirstConnectSuccessed = true;

            // 消息线程
            m_MessageThread = new Thread(MessageThread);
            m_MessageThread.IsBackground = true;
            m_MessageThread.Start();

            // 心跳线程
            m_HeartThread = new Thread(PingThread);
            m_HeartThread.IsBackground = true;
            m_HeartThread.Start();

            m_Contecting = false;

            // 请求密钥
            ProtocalManager.SecretKeyRequest();

            

            m_Socket.BeginReceive(m_ReadBuff.Bytes,m_ReadBuff.WriteIndex,m_ReadBuff.Remain,0, ReceiveCallback, socket);
        }
        catch (Exception ex)
        {

            Debug.LogError("Socket Coneect Fail: "+ex);
            m_Contecting = false;
        }

    }

    void ReceiveCallback(IAsyncResult ar) {
        try
        {
            Socket socket = (Socket)ar.AsyncState;
            int count = socket.EndReceive(ar);

            if (count <= 0)
            {
                // 关闭连接
                Close();

                return;
            }

            m_ReadBuff.WriteIndex += count;

            OnReceiveData();

            if (m_ReadBuff.Remain < 8)
            {
                m_ReadBuff.MoveBytes();
                m_ReadBuff.Resize(m_ReadBuff.Length *2);
            }

            socket.BeginReceive(m_ReadBuff.Bytes, m_ReadBuff.WriteIndex,m_ReadBuff.Remain,0,ReceiveCallback,socket);

        }
        catch (SocketException  ex)
        {
            Debug.LogError("Socket Receive Fail: " + ex);
            Close();
        }
    }

    /// <summary>
    /// 对数据进行处理
    /// </summary>
    void OnReceiveData() {
        if (m_ReadBuff.Length <=4 || m_ReadBuff.ReadIndex< 0)
        {
            return;
        }


        int readIndex = m_ReadBuff.ReadIndex;
        byte[] bytes = m_ReadBuff.Bytes;

        int bodyLength = BitConverter.ToInt32(bytes,readIndex);


        // 读取协议长度，然后进行判断，如果消息长度小于都出来的消息长度，证明不是一条完整的消息（分包现象）
        if (m_ReadBuff.Length < bodyLength +4)
        {
            return;
        }

        // 解析协议名称
        m_ReadBuff.ReadIndex += 4;
        int nameCount = 0;
        ProtocolEnum protocol = MessageBase.DecodeName(m_ReadBuff.Bytes,m_ReadBuff.ReadIndex, out nameCount);
        if (protocol == ProtocolEnum.None)
        {
            Debug.LogError("OnReceiveData MessageBase.DecodeName Fail");
            Close();
            return;
        }

        // 解析协议
        m_ReadBuff.ReadIndex += nameCount;
        int bodyCount = bodyLength - nameCount;

        try
        {
            MessageBase messageBase = MessageBase.Decode(protocol,m_ReadBuff.Bytes, m_ReadBuff.ReadIndex,bodyCount);
            if (messageBase ==null)
            {
                Debug.LogError("接收数据协议内容解析出错");
                Close();

                return;
            }

            m_ReadBuff.ReadIndex += bodyCount;
            m_ReadBuff.CheckAndMoveBytes();

            // 协议具体的操作
            lock (m_MessageList)
            {
                m_MessageList.Add(messageBase);
            }
            m_MessageCount++;

            // 粘包数据处理
            if (m_ReadBuff.Length>4)
            {
                OnReceiveData();
            }

        }
        catch (Exception ex)
        {

            Debug.LogError("Socket OnReceiveData Error: "+ex);
            Close();
        }
    }


    /// <summary>
    /// 发送数据到服务器
    /// </summary>
    /// <param name="messageBase"></param>
    public void SendMessage(MessageBase messageBase) {
        if (m_Socket ==null && m_Socket.Connected==false)
        {
            return;
        }

        if (m_Contecting)
        {
            Debug.LogError("正在连接服务器中，无法发送消息给服务器");
            return;
        }

        if (m_Closing)
        {
            Debug.LogError("正在关闭连接中，无法发送消息给服务器");
            return;
        }

        try
        {
            byte[] nameBytes = MessageBase.EncodeName(messageBase);
            byte[] bodyBytes = MessageBase.Encode(messageBase);
            int len = nameBytes.Length + bodyBytes.Length;
            byte[] byteHead = BitConverter.GetBytes(len);
            byte[] sendBytes = new byte[byteHead.Length +len];

            Array.Copy(byteHead,0,sendBytes,0,byteHead.Length);
            Array.Copy(nameBytes,0,sendBytes, byteHead.Length, nameBytes.Length);
            Array.Copy(bodyBytes,0,sendBytes, byteHead.Length + nameBytes.Length, bodyBytes.Length);
            ByteArray ba = new ByteArray(sendBytes);

            

            int count = 0;

            // 添加消息到队列
            lock (m_WriteQueue)
            {
                m_WriteQueue.Enqueue(ba);

                count = m_WriteQueue.Count;
            }

            if (count == 1)
            {
                
                m_Socket.BeginSend(sendBytes,0, sendBytes.Length,0, SendCallback, m_Socket);
            }

        }
        catch (Exception ex)
        {

            Debug.LogError("SendMessage Error : "+ex);
            Close();
        }
    }

    /// <summary>
    /// 发送消息（发送结束回调）
    /// </summary>
    /// <param name="ar"></param>
    void SendCallback(IAsyncResult ar) {
        try
        {
            Socket socket = (Socket)ar.AsyncState;

            if (socket == null && socket.Connected==false) {
                return;
            }

            int count = socket.EndSend(ar);

            // 判断是否发送完成
            ByteArray ba;
            lock (m_WriteQueue)
            {
                ba = m_WriteQueue.First();

            }

            ba.ReadIndex += count;
            //代表发送完成
            if (ba.Length ==0)
            {

                lock (m_WriteQueue)
                {
                    m_WriteQueue.Dequeue();
                    if (m_WriteQueue.Count > 0)
                    {
                        ba = m_WriteQueue.First();
                    }
                    else {
                        ba = null;
                    }
                }
            }

            // 发送不完整或者发送完整且存在第二条数据
            if (ba != null)
            {
                
                socket.BeginSend(ba.Bytes,ba.ReadIndex, ba.Length,0, SendCallback, socket);

            }
            else if (m_Closing)     // 确保关闭连接前，先把数据发送出去
            {
                RealClose();
            }

        }
        catch (Exception ex)
        {

            Debug.LogError("SendCallback Error : " + ex);
            Close();
        }
    }

    /// <summary>
    /// 关闭连接
    /// </summary>
    /// <param name="isNormal"></param>
    public void Close(bool isNormal = true) {
        if (m_Socket==null || m_Contecting)
        {
            return;
        }

        //
        if (m_Contecting)
        {
            return;

        }
        if (m_WriteQueue.Count > 0)
        {
            m_Closing = true;
        }
        else
        {
            RealClose(isNormal);

        }
      
    }

    /// <summary>
    /// 真正关闭
    /// </summary>
    void RealClose(bool isNormal = true) {
        Secretkey = "";
        m_Socket.Close();
        FirstEvent(NetEvent.Close, isNormal.ToString());

        //掉线
        m_Offline = true;

        if (m_HeartThread != null && m_HeartThread.IsAlive)
        {
            m_HeartThread.Abort();
            m_HeartThread = null;
        }

        if (m_MessageThread != null && m_MessageThread.IsAlive)
        {
            m_MessageThread.Abort();
            m_MessageThread = null;
        }

        Debug.Log("Close Socket");
    }

    /// <summary>
    /// 设置密钥
    /// </summary>
    /// <param name="key"></param>
    public void SetSecretkey(string key) {
        Secretkey = key;
    }

    /// <summary>
    /// 获得当前的时间戳
    /// </summary>
    /// <returns></returns>
    public static long GetTimeStamp()
    {

        TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);

        return Convert.ToInt64(ts.TotalSeconds);
    }
}
