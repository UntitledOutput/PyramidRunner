using System;
using System.Collections.Generic;
using MessagePack;
using UnityEngine;

public enum PacketType : ushort
{
    CONNECT = 0,
    DISCONNECT = 1,
    HEARTBEAT = 2,
    INSTANTIATE = 3,
    
    // ---
    
    OBJECT_UPDATE = 4,
    OBJECT_DESTROY = 5,
    
    // ---
    
    JOIN_ROOM = 10,
    LEAVE_ROOM = 11,
    ROOM_LIST = 12,
    START_ROOM = 13,
    CREATE_ROOM = 14,
    END_ROOM = 15,
    // ---
    
    PLAYER_ACTION = 21,
    PLAYER_ASSIGN = 22,
    PLAYER_INFO = 23,
    PLAYER_DAMAGE = 24,
    PLAYER_UPDATE = 25,
    
    TASK_COMPLETE = 26,
    
    // ---
    
    WORLD_STATE = 30,
    PLAYER_STATE = 31,
    SCENE_CHANGE = 32
}



[MessagePackObject]
public class Packet
{
    [Key(0)]
    public PacketType Type { get; set; }
    
    [Key(1)]
    public uint Sequence { get; set; }
    
    [Key(2)]
    public float Timestamp { get; set; }
    
    [Key(3)]
    public Dictionary<string, object> Data { get; set; }
    
    public const int HEADER_SIZE = 10;
    
    public byte[] Serialize()
    {
        Timestamp = 69;
        
        // Serialize data payload
        byte[] payload = MessagePackSerializer.Serialize(Data);
        
        // Build header: type (2 bytes), sequence (4 bytes), timestamp (4 bytes)
        byte[] packet = new byte[HEADER_SIZE + payload.Length];
        
        // Method 1: Manual byte packing (RECOMMENDED - more explicit)
        // Type (2 bytes, big endian)
        packet[0] = (byte)((ushort)Type >> 8);
        packet[1] = (byte)((ushort)Type & 0xFF);
        
        // Sequence (4 bytes, big endian)
        packet[2] = (byte)(Sequence >> 24);
        packet[3] = (byte)((Sequence >> 16) & 0xFF);
        packet[4] = (byte)((Sequence >> 8) & 0xFF);
        packet[5] = (byte)(Sequence & 0xFF);
        
        // Timestamp (4 bytes, big endian float)
        byte[] timeBytes = BitConverter.GetBytes(Timestamp);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(timeBytes);
        timeBytes.CopyTo(packet, 6);
        
        // Payload
        payload.CopyTo(packet, HEADER_SIZE);
        
        return packet;
    }
    
    public static Packet Deserialize(byte[] data)
    {
        if (data.Length < HEADER_SIZE)
            throw new Exception("Packet too small");
        
        // Convert from network byte order
        byte[] header = new byte[HEADER_SIZE];
        Array.Copy(data, 0, header, 0, HEADER_SIZE);
        
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(header, 0, 2);
            Array.Reverse(header, 2, 4);
            Array.Reverse(header, 6, 4);
        }
        
        ushort type = BitConverter.ToUInt16(header, 0);
        uint sequence = BitConverter.ToUInt32(header, 2);
        float timestamp = BitConverter.ToSingle(header, 6);
        
        byte[] payload = new byte[data.Length - HEADER_SIZE];
        Array.Copy(data, HEADER_SIZE, payload, 0, payload.Length);
        
        var dataDict = MessagePackSerializer.Deserialize<Dictionary<string, object>>(payload);
        
        return new Packet
        {
            Type = (PacketType)type,
            Sequence = sequence,
            Timestamp = timestamp,
            Data = dataDict
        };
    }
}