using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;



public class thirds_client : MonoBehaviour, IProtagoras_Client<object>
{
    public bool debug = true;
    int host;
    int conn_id;
    int reliable_channel;
    int unreliable_channel;
    int all_cost_channel;
    int state_update_channel;
    int large_data_channel;
    List<List<string>> party_list = new List<List<string>>();
    Action<string> invite_event;
    Action<string> request_event;
    Action<object> message_event;
    bool connected = false;
    HostTopology topology;


    void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    IEnumerator Receive()
    {
        int _conn;
        int _channel;
        byte[] _buffer = new byte[2048];
        int data_size;
        byte error;
        Message_obj msg = new Message_obj();
        Message_package msg_p = new Message_package();

        while (true)
        {
            yield return null;
            NetworkEventType _data =
                NetworkTransport.ReceiveFromHost(host, out _conn, out _channel, _buffer, 2048, out data_size, out error);
            switch (_data)
            {
                case NetworkEventType.DataEvent:
                    msg_p = unformat_bytes<Message_package>(_buffer);
                    if (msg_p.message is Message_obj)
                    {
                        msg = (Message_obj)msg_p.message;
                    }
                    handle_message(msg_p.type,msg,msg_p.message);
                    break;
                case NetworkEventType.ConnectEvent:
                    if(debug) print($"conected on channel: {_channel}, with error: {(NetworkError)error} on conn: {_conn}");
                    if(_conn == conn_id) connected = true;
                    break;
                case NetworkEventType.DisconnectEvent:
                    if (debug) print($"didn't connect: {(NetworkError)error}");
                    break;
            }
        }
    }

    void handle_message(Custom_msg_type _type, Message_obj msg, object message)
    {
        switch (_type)
        {
            case Custom_msg_type.CREATE_PLAYER:
                //make the server sent this back if you do create a player
                //then make our create player function call an ienumerator that 
                //yields until we recieve this message. That way we can make 
                //create_player only return true iff we create a player.
                break;
            case Custom_msg_type.LOGIN:
                break;
            case Custom_msg_type.SEND_PLAYER_LIST:
                //ignore this for now
                break;
            case Custom_msg_type.SEND_PARTY_LIST:
                party_list = (List<List<string>>)(message);
                if(party_list == null)
                {
                    party_list = new List<List<string>>();
                }
                break;
            case Custom_msg_type.LEAVE_PARTY:
                break;
            case Custom_msg_type.REQ_JOIN_PARTY:
                if(debug) print($"Got a req event with {msg.arg1}");
                request_event(msg.arg1);
                break;
            case Custom_msg_type.INVITE_PLAYER:
                invite_event(msg.arg1);
                break;
            case Custom_msg_type.START_GAME:
                message_event("your game started");
                break;
            case Custom_msg_type.LOGOUT:
                break;
            case Custom_msg_type.MTC:
                message_event(message);
                break;
            case Custom_msg_type.RPC:
                break;
            case Custom_msg_type.CMD:
                break;
            case Custom_msg_type.END_GAME:
                message_event("your game is over");
                break;
        }
    }

    bool send_message(Custom_msg_type type, string arg1, string arg2, int targetConnection, object body = null)
    {
        byte error = 0;
        Message_package msg_p = new Message_package();
        Message_obj msg = new Message_obj();
        msg_p.type = type;
        msg.arg1 = arg1;
        msg.arg2 = arg2;
        msg.target_connection = targetConnection;
        if (body == null)
        {
            msg_p.message = msg;
        }
        else
        {
            msg_p.message = body;
        }

        byte[] data = format_data(msg_p);
        NetworkTransport.Send(host, conn_id, reliable_channel, data, data.Length, out error);
        if(debug) print($"trying to send {type}: {(NetworkError)error} at channel: {reliable_channel}" +
            $" on host {host} on conn {conn_id}");
        return (NetworkError)error == NetworkError.Ok;
    }

    byte[] format_data<T>(T obj)
    {
        using (MemoryStream mem = new MemoryStream())
        {
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(mem, obj);
            return mem.ToArray();
        }
    }

    T unformat_bytes<T>(byte[] bytes)
    {
        using (MemoryStream mem = new MemoryStream(bytes))
        {
            mem.Position = 0;
            BinaryFormatter bf = new BinaryFormatter();
            object obj = bf.Deserialize(mem);
            return (T)obj;
        }
    }


    public bool Connect(int port)
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliable_channel = config.AddChannel(QosType.Reliable);
        unreliable_channel = config.AddChannel(QosType.Unreliable);
        all_cost_channel = config.AddChannel(QosType.AllCostDelivery);
        state_update_channel = config.AddChannel(QosType.StateUpdate);
        large_data_channel = config.AddChannel(QosType.ReliableFragmentedSequenced);
        topology = new HostTopology(config, 100);

        host = NetworkTransport.AddHost(topology, 0);
        byte error;
        //progatoras is running on 15150.
        conn_id = NetworkTransport.Connect(host, "71.61.58.16", 15150, 0, out error);
        if (debug) print($"connecting {(NetworkError)error}");
        StartCoroutine(Receive());
        return (NetworkError)error == NetworkError.Ok;

    }

    public bool Create_Player(string name, string password)
    {
        return send_message(Custom_msg_type.CREATE_PLAYER, name, null, -1);
    }

    public bool End_Game()
    {
        return send_message(Custom_msg_type.END_GAME, "", null, -1);
    }

    public List<Party_Names> Get_Party_list()
    {
        if (party_list == null) party_list = new List<List<string>>();

        return party_list.Select(L => new Party_Names(L.First(),L.Skip(1).ToList())).ToList();
    }

    public bool Invite_Player(string name)
    {
        return send_message(Custom_msg_type.INVITE_PLAYER, name, null, -1);
    }

    public bool Join_Party(string name)
    {
        return send_message(Custom_msg_type.REQ_JOIN_PARTY, name, null, -1);
    }

    public bool Login(string name, string password)
    {
        return send_message(Custom_msg_type.LOGIN, name, null, -1);
    }

    public bool Logout()
    {
        return send_message(Custom_msg_type.LOGOUT, "", null, -1);
    }

    public void Register_Receive_Invite(Action<string,Action> when_you_are_invited)
    {
        invite_event = str => when_you_are_invited(str,() => Join_Party(str));
    }

    public void Register_Receive_Request(Action<string,Action> when_someone_wants_to_join)
    {
        request_event = str => when_someone_wants_to_join(str, () => Invite_Player(str));
    }


    public bool Start_Game()
    {
        return send_message(Custom_msg_type.START_GAME, "", "", -1);
    }

    public bool Setup_for_player(string name, string password, Action<string,Action> invite_trigger, Action<string,Action> request_trigger, Action<object> message_trigger, int port)
    {
        Register_Receive_Invite(invite_trigger);
        Register_Receive_Request(request_trigger);
        Register_Message_Receive(message_trigger);
        StartCoroutine(connect_prodecure(name,password));
        return Connect(port);

    }

    IEnumerator connect_prodecure(string name, string password)
    {
        yield return new WaitUntil(() => connected);
        if(debug) print("we connected");
        Create_Player(name, password);
        Login(name, password);

    }
    public thirds_client(string name, string password, Action<string,Action> invite_trigger, Action<string,Action> request_trigger, Action<string> message_trigger, int port)
    {
        //Setup_for_player(name, password, invite_trigger, request_trigger, message_trigger, port);
    }

    public bool Multicast(object msg)
    {
        return send_message(Custom_msg_type.MTC, "", "", -1,body: msg);
    }

    public void Register_Message_Receive(Action<object> when_you_receive_message)
    {
        message_event = when_you_receive_message;
    }

}