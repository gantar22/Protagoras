﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

public enum mtc_Type : byte { player_state, dagger_throw, dagger_die, player_die }


[System.Serializable]
public struct mtc_data
{
    public mtc_Type type;
    public float t;
    public object body;
    public int id;

    public mtc_data(mtc_Type type, float t, object body, int id)
    {
        this.type = type;
        this.t = t;
        this.body = body;
        this.id = id;
    }
}

[System.Serializable]
public class serializable_vec2
{
    public float x;
    public float y;

    public serializable_vec2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    public static implicit operator Vector2(serializable_vec2 sv2)
    {
        return new Vector2(sv2.x, sv2.y);
    }

    public static implicit operator serializable_vec2(Vector2 v2)
    {
        return new serializable_vec2(v2.x, v2.y);
    }

    public static implicit operator Vector3(serializable_vec2 sv2)
    {
        return new Vector3(sv2.x, sv2.y);
    }


}

[System.Serializable]
public struct player_state
{
    public serializable_vec2 pos;
    public serializable_vec2 vel;

    public player_state(Vector2 pos, Vector2 vel)
    {
        this.pos = pos;
        this.vel = vel;
    }
}

enum Custom_msg_type : byte
{ CREATE_PLAYER, LOGIN, SEND_PLAYER_LIST, SEND_PARTY_LIST, LEAVE_PARTY, REQ_JOIN_PARTY, INVITE_PLAYER, START_GAME, LOGOUT, MTC, RPC, CMD, END_GAME }

[System.Serializable]
struct Message_package
{
    public object message;
    public Custom_msg_type type;
}

[System.Serializable]
struct Message_obj
{
    public string arg1;
    public string arg2;
    public int target_connection;
}

[System.Serializable]
public class Party_info //implement ISerializable?
{
    public bool leader_authority;
    public List<int> players;
    public int leader;
    public List<int> requested_players; //TODO make this a Dict [wtf carter this should be an array]
    public List<int> invited_players;   //no just make it sql. Also make a data oreinted version with less overhead

    public override string ToString()
    {
        var r = players.Select(x => x.ToString()).Aggregate(" ",(acc,x) => acc + x);
        return $"party with the players: {r}";
    }

    public Party_info(int leader,bool leader_authority = false)
    {
        this.leader_authority = leader_authority;
        this.players = new List<int>();
        players.Add(leader);
        this.leader = leader;
        this.requested_players = new List<int>();
        this.invited_players = new List<int>();
    }
}


[System.Serializable]
public class Connection_status
{
    public string name;
    public int conn_id;
    public bool is_in_game;

    public override string ToString()
    {
        return $"<- player: {name} #{conn_id} is {(is_in_game ? "" : "not")} in game ->\n";
    }


    public Connection_status(string name, int conn_id)
    {
        this.name = name;
        this.conn_id = conn_id;
        is_in_game = false;
    }
}






public class User_info
{
    public string name;
    public List<string> friends;
    public string hashed_password;

    public User_info(string name, string hashed_password)
    {
        this.name = name;
        this.hashed_password = hashed_password;
        friends = new List<string>();
    }
}
public class Party_manager
{
    bool resend_msg = true;

    Dictionary<string, User_info> login_table; //serialize this to a local file so we can reboot

    Dictionary<int, Party_info> party_table;//conn_id to party

    Dictionary<int, Connection_status> player_table;


    public List<List<string>> get_parties()
    {
        return party_table
            .Values
            .Select(party => 
            (new List<string> { player_table[party.leader].name })
            .Concat((party.players.Select(id => player_table[id].name)
            .Where(name => name != player_table[party.leader].name))
            .ToList()).ToList())
            .ToList();
    }

    public int get_conn_id(string name)
    {
        return player_table.Values.Where(status => status.name == name).First().conn_id;
    }
    public string get_name(int conn_id) { return player_table[conn_id].name; }
    public List<Connection_status> get_players()
    { return player_table.Values.ToList(); }

    public void init()
    {
        login_table  = new Dictionary<string, User_info>();
        party_table  = new Dictionary<int, Party_info>();
        player_table = new Dictionary<int, Connection_status>();
    }
    public bool create_player(string name, string hashed_password)
    {
        if(login_table.ContainsKey(name))
        {
            return false;
        }

        login_table[name] = new User_info(name, hashed_password);

        return true;
    }
     
    public bool login_player (string name, string hashed_password, int conn_id)
    {
        if(login_table[name].hashed_password != hashed_password)
        {
            return false;
        }

        if(player_table.Values.ToList().Any(status => status.name == name))
        {
            return false;
        }

        if (player_table.Keys.Contains(conn_id))
        {
            return false;
        }

        player_table[conn_id] = new Connection_status(name, conn_id);
        party_table[conn_id] = new Party_info(conn_id);

        return true;
    }
     
    public bool logout_player(int conn_id)
    {
        if(!player_table.Keys.Contains(conn_id))
        {
            return false;
        }
        leave_party(conn_id);
        
        remove_from_invite_lists(conn_id);
        remove_from_request_lists(conn_id);
        party_table.Remove(conn_id);
        player_table.Remove(conn_id);
        //this is gonna suck algorithmically unless players keep track off their own party interactions too

        return true;
    }
     
    public bool request_join (int requester, int requestee,Action send_request_msg)
    {
        if(!has_authority(requestee))
        {
            return false;
        }
        Party_info requested_party = party_table[requestee];
        if(requested_party.requested_players.Contains(requester) && !resend_msg)
        {
            return false;
        }
        
        if(requested_party.invited_players.Contains(requester)) //TODO check for friendship
        {

            join_party(requester, requested_party);
            return true;
        }


        requested_party.requested_players.Add(requester);
        
        //The requester has not been invited, so now inform the party leader to accept the request (invite them)
        send_request_msg(); //maybe make this string player input (unsafe)

        return true;
    }
     
    public bool invite_player(int inviter, int invitee, Action send_invite_msg)
    {
        if(party_table[inviter].invited_players.Contains(invitee) && !resend_msg)
        {
            return false;
        }
        if(!has_authority(inviter))
        {
            return false;
        }
        Party_info inviting_party = party_table[inviter];

        if(inviting_party.requested_players.Contains(invitee)) //friendship
        {
            join_party(invitee, inviting_party);
            return true;
        }

        inviting_party.invited_players.Add(invitee);
        send_invite_msg();

        return false;
    }
     
    public bool accept_invitation(int invitee, int inviter)
    {
        //called when send_invite_msg gets responded to
        if(!party_table.ContainsKey(inviter))
        {
            return false;
        }

        party_table[inviter].invited_players.Remove(invitee);
        join_party(invitee, party_table[inviter]);
        return true;
    }
     
    public bool accept_request(int requestee, int requester)
    {
        if(!party_table.ContainsKey(requestee))
        {
            return false;
        }

        join_party(requester, party_table[requester]); //this will get them out of the requested player list
        return true;
    }
     
    public bool kick_player(int kicker, int kickee)
    {
        if(!has_authority(kicker))
        {
            return false;
        }

        if(!party_table[kicker].players.Contains(kicker))
        {
            return false;
        }

       
        leave_party(kickee);
        party_table[kicker].invited_players.Remove(kickee);
        return true;
    }
     
    public bool merge_parties(Party_info source, Party_info dest)
    {
        int player;
        for(int i = 0; i < source.players.Count; i++)
        {
            player = source.players[i];
            //leave_party(player); //we remove the party leader everytime this is a little expensive instead do this in reverse
            join_party(player, dest, true); //DEBUG if one of these fails, return false
        }
        return true;
    } 
     
    public void remove_from_request_lists(int conn_id)
    {
        for(int i = 0; i < party_table.Values.Count; i++)
        {
            party_table.Values.ToList()[i].requested_players.Remove(conn_id);
        }
    }
     
    public void remove_from_invite_lists(int conn_id)
    {
        for (int i = 0; i < party_table.Values.Count; i++)
        {
            party_table.Values.ToList()[i].invited_players.Remove(conn_id);
        }
    }
     
    public bool has_authority(int conn_id)
    {
        return !party_table[conn_id].leader_authority || party_table[conn_id].leader == conn_id;

    }
    
    public void join_party(int joiner, Party_info party, bool no_merge = false)
    {
        remove_from_request_lists(joiner); //they are content being in the new party
        //its okay if a party is "inviting" memebers that are in that party,
        //because that means they can leave and come back freely, which is good
        if(!no_merge && has_authority(joiner))
        {
            merge_parties(party_table[joiner], party);
        } else
        {
            leave_party(joiner);
            party.players.Add(joiner);
            party_table[joiner] = party;
        }
    } 
    
    public void leave_party(int leaver)
    {
        if(party_table[leaver].leader == leaver && party_table[leaver].players.Count > 1) // leaders are in players
        {
            party_table[leaver].leader = party_table[leaver].players.Where(player => player != leaver).FirstOrDefault();
        }

        party_table[leaver].invited_players.Add(leaver); //the leaver should be able to come home
        party_table[leaver].players.Remove(leaver);
        if(party_table[leaver].players.Count == 0) party_table.Remove(leaver);
        party_table[leaver] = new Party_info(leaver);

    }

    public bool start_game(int starter,Action<List<int>> start_msg)
    {
        if(!has_authority(starter))
        {
            return false;
        }
        for(int i = 0; i < party_table[starter].players.Count; i++)
        {
            player_table[party_table[starter].players[i]].is_in_game = true;
        }
        //multicast(starter, start_msg);
        multicast(-1, start_msg);
        return true;
    }

    public bool end_game(int ender,Action<List<int>> end_msg) 
    {
        if(party_table[ender].leader != ender) //you have to be the leader to end the game
        {
            return false;
        }
        for(int i = 0;i < party_table[ender].players.Count;i++)
        {
            player_table[party_table[ender].players[i]].is_in_game = false;
        }
        end_msg(party_table[ender].players);
        return true;
    }

    public void inform_lobby_players(Action<List<int>> send_to_lobby_players)
    {
       
        send_to_lobby_players(player_table.Keys.Where(id => !player_table[id].is_in_game).ToList());
    }

    public void command(int conn_id, Action<List<int>> send_msg_to)
    {
        send_msg_to(new List<int>(new int[] { party_table[conn_id].leader }));
    }

    public void multicast(int conn_id, Action<List<int>> send_msg_to)
    {
        send_msg_to(party_table[conn_id].players.Where(id => id != conn_id).ToList());
    }

    public void rpc(int conn_id,Action<List<int>> send_msg_to)
    {
        if (party_table[conn_id].leader != conn_id) return;
        multicast(conn_id, send_msg_to);
    }

    
}



public class Protagoras : MonoBehaviour {
    int reliable_channel;
    int unreliable_channel;
    int all_cost_channel;
    int state_update_channel;
    int large_data_channel;

    HostTopology topology;
    int host;

    const int port = 15150;
    const int MAX_PLAYERS = 100;

    Party_manager pm;

    





    // Use this for initialization
    void Start () {
        NetworkTransport.Init();

        ConnectionConfig config = new ConnectionConfig();
        reliable_channel     = config.AddChannel(QosType.Reliable);
        unreliable_channel   = config.AddChannel(QosType.Unreliable);
        all_cost_channel     = config.AddChannel(QosType.AllCostDelivery);
        state_update_channel = config.AddChannel(QosType.StateUpdate);
        large_data_channel   = config.AddChannel(QosType.ReliableFragmentedSequenced);

        topology = new HostTopology(config,MAX_PLAYERS);
        host = NetworkTransport.AddHost(topology, port);
        //print("server host:");
        //print(host);


        pm = new Party_manager();
        pm.init();
        StartCoroutine(receive());
        StartCoroutine(inform_players());
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

    T unformat_bytes<T> (byte[] bytes)
    {
        using (MemoryStream mem = new MemoryStream(bytes))
        {
            mem.Position = 0;
            BinaryFormatter bf = new BinaryFormatter();
            object obj = bf.Deserialize(mem);
            return (T)obj;
        }
    }

    Action send_action(Custom_msg_type _type, int actor,int recipient,int channel_id, int host_id) 
    {
        byte[] data = new byte[1024];
        Message_obj msg = new Message_obj();
        msg.target_connection = actor;
        msg.arg1 = pm.get_name(actor); //arg2 could be a custom message?
        Message_package msg_p = new Message_package();
        msg_p.type = _type;
        msg_p.message = msg;
        data = format_data(msg_p);
        byte error = (byte)0;

        return (() => NetworkTransport.Send(host_id, recipient, channel_id, data, data.Length, out error));
    }

    Action<List<int>> start_end_action(Custom_msg_type _type, int channel_id,int host_id)
    {
        byte[] data = new byte[1024];
        Message_package msg_p = new Message_package();
        msg_p.type = _type;

        data = format_data(msg_p);

         byte error = 0;

        return (players =>
        {//figure out how to multicast TODO
            foreach(int p in players)
            {
                NetworkTransport.Send(host_id, p, channel_id, data, data.Length, out error);
            }
        });
    }

    Action<List<int>> forward(int channel_id, int host_id, byte[] data)
    {
        byte error = 0;

        return (players =>
        {//figure out how to multicast TODO
            foreach (int p in players)
            {
                //print($"fowarding {data.Length} bytes to {p}");
                NetworkTransport.Send(host_id, p, channel_id, data, data.Length, out error);
                //print($"with error: {(NetworkError)error}");
            }
        });

    }

    IEnumerator handle_connect(Custom_msg_type _type, Message_obj msg, int len, int conn_id,int host,int channel_id,byte[] raw_msg)
    {


        switch (_type)
        {
            case Custom_msg_type.CREATE_PLAYER:
                string name_create = msg.arg1;
                string hashedpassword_create = msg.arg2;

                pm.create_player(name_create, hashedpassword_create);
                break;
            case Custom_msg_type.LOGIN:
                string name = msg.arg1;
                string hashed_password = msg.arg2;
                print($"login as name {name} and pass {hashed_password}");

                if(!pm.login_player(name, hashed_password, conn_id))
                { print("didn't add them"); }
                //print("logged in");
                break;
            case Custom_msg_type.SEND_PLAYER_LIST:
                //out
                //need a action that sends pm.player_table and pm.party_table

                //              scatch this one

                //pm.inform_lobby_players

                break;
            case Custom_msg_type.LEAVE_PARTY:
                pm.leave_party(conn_id);
                break;

            case Custom_msg_type.REQ_JOIN_PARTY:
                print("request join");
                //requestee
                //action that send request msg
                int requestee = 0;
                if (msg.arg1 != "")
                {
                    requestee = pm.get_conn_id(msg.arg1);
                } else
                {
                    requestee = msg.target_connection;
                }
                
                                               
                print(pm.request_join(conn_id, requestee, send_action(_type,conn_id, requestee, channel_id, host)));
                //pm.request_join 
                break;
            case Custom_msg_type.INVITE_PLAYER:
                //invitee
                //action that sends invite msg
                int invitee = 0;
                if (msg.arg1 != "")
                {
                    invitee = pm.get_conn_id(msg.arg1);
                }
                else
                {
                    invitee = msg.target_connection;
                }
                pm.invite_player(conn_id, invitee, send_action(_type, conn_id, invitee, channel_id, host));
                

                //pm.invite_player
                break;
            case Custom_msg_type.START_GAME:
                //action that sends start game msg to clients


                pm.start_game(conn_id, start_end_action(Custom_msg_type.START_GAME,channel_id,host));
                //pm.start_game
                break;
            case Custom_msg_type.END_GAME:

                pm.end_game(conn_id, start_end_action(Custom_msg_type.END_GAME, channel_id, host));
                break;
            case Custom_msg_type.LOGOUT:
                print($"loging out {conn_id}");
                pm.logout_player(conn_id);
                break;
            case Custom_msg_type.MTC:
                pm.multicast(conn_id, forward(channel_id, host, raw_msg));
                break;
            case Custom_msg_type.RPC:
                pm.rpc(conn_id, forward(channel_id, host, raw_msg));
                break;
            case Custom_msg_type.CMD:
                pm.command(conn_id, forward(channel_id, host, raw_msg));
                break;
        }
        //print("leaving handle");
        yield return null;
        //print("back in handle");
    }


    // f(x) => a = 0; return f(x-1) 

    IEnumerator receive() {
        int _conn;
        int _channel;
        byte[] _buffer = new byte[2048];
        int data_size;
        byte error;

        while (true)
        {
            //print("receiving");
            NetworkEventType _data =
                NetworkTransport.ReceiveFromHost(host, out _conn, out _channel, _buffer, 2048, out data_size, out error);
            //print($"recieved {_data}");
            switch (_data)
            {
                case NetworkEventType.Nothing:
                    break;
                case NetworkEventType.ConnectEvent:
                    break;
                case NetworkEventType.DataEvent: //case on channels here? like state_update channels should just go straight back out
                    Message_package msg_p = unformat_bytes<Message_package>(_buffer);
                    Message_obj msg = new Message_obj();
                    if (msg_p.message is Message_obj)
                    { //make sure msg_p.type is mtc rpc or cmd
                        msg = (Message_obj)msg_p.message;
                    }
                    StartCoroutine(handle_connect(msg_p.type,msg, data_size - 1, _conn,host,_channel,_buffer));
                    _buffer = new byte[1024];
                    break;
                case NetworkEventType.DisconnectEvent:
                    print($"disconnect from {_conn} with error {(NetworkError)error}");
                    pm.logout_player(_conn);
                    break;
            }
            yield return null;

        }
    }
    class adhoc_comp : IEqualityComparer<List<string>>
    {
        public bool Equals(List<string> x, List<string> y)
        {
            return x.FirstOrDefault() == y.FirstOrDefault();
        }

        public int GetHashCode(List<string> obj)
        {
            return obj.GetHashCode();
        }
    }
    IEnumerator inform_players()
    {
        int send_rate = 2;
        byte[] data;
        Message_package msg;

        while (true)
        {
            try
            {
                msg = new Message_package();
                msg.type = Custom_msg_type.SEND_PARTY_LIST;
                msg.message = pm.get_parties().GroupBy(l => l.FirstOrDefault()).Select(g => g.First()).ToList();
                data = format_data(msg);

                pm.inform_lobby_players(forward(large_data_channel, host, data));
            }
            catch (NullReferenceException e) { print($"null ref in party search: {e}"); }
            finally { }

            yield return new WaitForSeconds(send_rate);

        }
    }
}
