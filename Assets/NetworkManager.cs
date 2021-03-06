﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System;
using System.Linq;
using System.Threading;
using System.IO;
using System.Reflection;

public enum NetworkState
{
    Disconnected,
    Client,
    Server
}

public class NetworkPacketId : Attribute
{
    public byte PacketID { get; private set; }
    public NetworkPacketId(byte id)
    {
        PacketID = id;
    }
    public NetworkPacketId(ClientIdentifiers cid)
    {
        PacketID = (byte)cid;
    }
    public NetworkPacketId(ServerIdentifiers sid)
    {
        PacketID = (byte)sid;
    }
}

public class NetworkManager : MonoBehaviour {

    private static NetworkManager _Instance = null;
    public static NetworkManager Instance
    {
        get
        {
            if (_Instance == null) _Instance = FindObjectOfType<NetworkManager>();
            return _Instance;
        }
    }

    public static bool IsServer
    {
        get
        {
            return (Instance.State == NetworkState.Server);
        }
    }

    public static bool IsClient
    {
        get
        {
            return (Instance.State == NetworkState.Client);
        }
    }

    public NetworkState State { get; private set; }

    private void InitGeneric(ushort port)
    {

    }

    public void Start()
    {

    }

    public bool InitServer(ushort port)
    {
        if (State != NetworkState.Disconnected)
            Disconnect();

        InitGeneric(port);

        if (ServerManager.Init(port))
        {
            State = NetworkState.Server;
            return true;
        }

        return false;
    }

    public bool InitClient(string addr, ushort port)
    {
        if (State != NetworkState.Disconnected)
            Disconnect();

        InitGeneric(0); // init with default port

        if (ClientManager.Init(addr, port))
        {
            State = NetworkState.Client;
            return true;
        }

        return false;
    }

    public void Disconnect()
    {
        if (State == NetworkState.Server)
            ServerManager.Shutdown(false);
        if (State == NetworkState.Client)
            ClientManager.Shutdown(false);
        State = NetworkState.Disconnected;
    }

    public void OnDestroy()
    {
        ServerManager.Shutdown(true);
        ClientManager.Shutdown(true);
    }

    private static int mCurrentIn = 0;
    private static int mCurrentOut = 0;
    private static float mCurrentTime = 0;
    public static int mLastIn = 0;
    public static int mLastOut = 0;

    public void Update()
    {
        ServerManager.Update();
        ClientManager.Update();

        mCurrentTime += Time.unscaledDeltaTime;
        if (mCurrentTime >= 1)
        {
            mLastIn = mCurrentIn;
            mLastOut = mCurrentOut;
            mCurrentIn = 0;
            mCurrentOut = 0;
            mCurrentTime = 0;
        }
    }

    private static byte[] DoReadDataFromStream(Socket sock, int size)
    {
        try
        {
            byte[] ovtmp = new byte[size];
            byte[] ov = new byte[size];
            int done = 0;
            while (true)
            {
                if (!sock.Poll(0, SelectMode.SelectRead))
                {
                    Thread.Sleep(1);
                    continue;
                }
                if (sock.Available == 0)
                    return null; // disconnected
                int doneNow = Math.Max(0, sock.Receive(ovtmp, size - done, SocketFlags.None));
                ovtmp.Take(doneNow).ToArray().CopyTo(ov, done);
                done += doneNow;
                mCurrentIn += doneNow;
                if (done >= size)
                {
                    /*using (FileStream fs = File.Open("recvDbg.bin", FileMode.Append, FileAccess.Write))
                        fs.Write(ov, 0, ov.Length);*/
                    return ov;
                }
            }
        }
        catch(Exception e)
        {
            Debug.Log(e.ToString());
            return null;
        }
    }

    public static byte[] DoReadPacketFromStream(Socket sock)
    {
        // first off, try reading 4 bytes
        byte[] packet_size_buf = DoReadDataFromStream(sock, 4);
        if (packet_size_buf == null)
            return null;
        uint packet_size = BitConverter.ToUInt32(packet_size_buf, 0);
        if (packet_size > 65535) // separate packets of size larger than 65535 shouldn't exist. this is a protection against memory allocation DOS by the clients.
            return null;
        byte[] packet_buf = DoReadDataFromStream(sock, (int)packet_size);
        return packet_buf;
    }

    private static bool DoWriteDataToStream(Socket sock, byte[] data)
    {
        /*using (FileStream fs = File.Open("sendDbg.bin", FileMode.Append, FileAccess.Write))
            fs.Write(data, 0, data.Length);*/
        try
        {
            int done = 0;
            while (true)
            {
                if (!sock.Poll(0, SelectMode.SelectWrite))
                {
                    Thread.Sleep(1);
                    continue;
                }
                byte[] ov = data.Skip(done).Take(1024).ToArray();
                int doneNow = sock.Send(ov);
                done += doneNow;
                mCurrentOut += doneNow;
                if (done == data.Length)
                    return true;
            }
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            return false;
        }
    }

    public static bool DoWritePacketToStream(Socket sock, byte[] packet)
    {
        if (!DoWriteDataToStream(sock, BitConverter.GetBytes((uint)packet.Length)))
            return false;
        if (!DoWriteDataToStream(sock, packet))
            return false;
        return true;
    }

    // types are unlikely to change IN THIS CASE
    private static List<Type> PacketTypes = null;
    public static Type FindTypeFromPacketId(string ns, byte pid)
    {
        if (PacketTypes == null)
        {
            PacketTypes = new List<Type>();
            Type[] types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (Type type in types)
            {
                NetworkPacketId[] npi = (NetworkPacketId[])type.GetCustomAttributes(typeof(NetworkPacketId), false);
                if (npi.Length <= 0)
                    continue;
                PacketTypes.Add(type);
            }
        }

        foreach (Type type in PacketTypes)
        {
            if (type.Namespace != ns)
                continue;
            NetworkPacketId[] npi = (NetworkPacketId[])type.GetCustomAttributes(typeof(NetworkPacketId), false);
            if (npi[0].PacketID == pid)
                return type;
        }

        return null;
    }
}
