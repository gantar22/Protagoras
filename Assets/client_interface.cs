using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


public struct Party_Names
{
    public string leader;
    public List<string> members;

    public Party_Names(string leader, List<string> members)
    {
        this.leader = leader;
        this.members = members;
    }
}

public interface IProtagoras_Client<Message_Type>
{
    bool Setup_for_player(string name, string password, Action<string,Action> invite_trigger, Action<string,Action> request_trigger,Action<Message_Type> message_trigger, int port);

    /*
     *  Params:   port is the port that you will listen on, this must be different 
     *            for each client on the machine
     *  Requires: You haven't called this before
     *  Ensures:  Makes a host and connects it to the relay server.
     *            Allows you to call all of the functions below.
     *            Return true iff the connection succeeds
     */
    bool Connect(int port);

    /*
     * Params:   name the players name, password the desired plaintext password
     * Requires: you called Connect
     * Ensures:  returns true if (name,password) will be registered
     *           with the server or they already were registered.
     */
    bool Create_Player(string name, string password);


    /*
     * Params:   name the players name, password the desired plaintext password
     * Requires: you called Connect, at some point create_player returned true
     *           for this name + password combo
     * Ensures:  The player will enter the list of connected player
     *           They will start receiving information about other connected players
     *           and their parties
     *           After this, the client program will join parties with this account
     *           and invite players for this account
     * 
     */
    bool Login(string name, string password);


    /*
     * Params:   the name of the person whose party you want to join
     * Requires: either the server is not in leader-authority mode or name is the leader
     *           You have logged in.
     * Ensures:  When name invites you to their party, you will join, unless you join a different party
     *           The function registered with Register_Receive_Request on name's client will be called.
     *           Returns false soemtimes if something goes wrong
     *            
     */
    bool Join_Party(string name);


    /*
     * Same but for inviting a player to your party
     * 
     */
    bool Invite_Player(string name);


    /*
     * Param:    -
     * Requires: you are logged in
     * Ensures:  you are not logged in
     *           returns false sometimes if something goes wrong
     * 
     */
    bool Logout();


    /*
     * 
     * Params:   -
     * Requires: You are logged in and not currently in a game
     * Ensures:  You will stop receiving invites and requests and the party list
     *           returns false sometimes if something goes wrong
     */
    bool Start_Game();
    

    /*
     * symmetric to start game. Don't call this if you aren't in a game.
     */
    bool End_Game();


    /*
     * Params: when_you_are_invited is a function which will set up ui for invites
     * Requires: -
     * Ensures: when_you_are_invited gets called when you are invited to a party
     *          and its string parameter is the name of the party leader who invited you
     *          the action is what you want to call, when the user hits yes
     * 
     */
    void Register_Receive_Invite (Action<string,Action> when_you_are_invited);


    /*
     * Equivalent for the above
     */
    void Register_Receive_Request(Action<string,Action> when_someone_wants_to_join);


    /*
     * Params:   -
     * Requires: You have logged in and have not called Start_Game
     * Ensures:  Gives you the most recent list that the server sent us
     *            
     */
    List<Party_Names> Get_Party_list();

    /*
     * Params:   msg is the object you want all other (not you) players
     *           in your party to receive 
     * Requires: You are logged in
     * Ensures:  All other players in your party will have their when_you_receive_message
     *           called with msg
     */
    bool Multicast(Message_Type msg);


    /*
     * Params:    when_you_receive_message will get called with msg's when you receive them
     * 
     */
    void Register_Message_Receive(Action<Message_Type> when_you_receive_message);
}
