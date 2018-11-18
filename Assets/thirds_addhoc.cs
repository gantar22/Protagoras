using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;
using System.Linq;
using UnityEngine.Events;

public class thirds_addhoc : MonoBehaviour {


    public Text party_list;
    public Button request_button;
    public GameObject carter_go;
    public GameObject dom_go;

    // Use this for initialization
    void Start () {
        StartCoroutine(test());
	}

    void Update()
    {
        
    }

    IEnumerator test()
    {
        yield return new WaitForSeconds(1);
        print($"carter connects: {carter_go.GetComponent<thirds_client>().Setup_for_player("xX_memes_Xx", "hunter2", invite_ui, request_ui, message_ui, 0)}");
        print($"dom connects: {dom_go.GetComponent<thirds_client>().Setup_for_player("Dom", "Ab@o9^N", invite_ui, request_ui, message_ui, 15248)}");


        StartCoroutine(populate_party_list());

        yield return new WaitForSeconds(3);
        carter_go.GetComponent<thirds_client>().Join_Party("Dom");
    }


    void message_ui(object message)
    {
        print(message);
    }
    void request_ui(string requester, Action accept)
    {
        request_button.onClick.AddListener(new UnityAction(accept));
        request_button.onClick.AddListener(new UnityAction(send_msg));
        request_button.gameObject.SetActive(true);
    }
    void send_msg()
    {
        carter_go.GetComponent<thirds_client>().Multicast("hello");
    }
    void invite_ui(string inviter, Action accept)
    {

    }
    IEnumerator populate_party_list()
    {
        while(true)
        {
            if (carter_go.GetComponent<thirds_client>().Get_Party_list().Count < 1)
            {
                party_list.text = "";
            } else
            {
                party_list.text = carter_go.GetComponent<thirds_client>().Get_Party_list().
                Select(obj => obj.leader + ": \n \t" + obj.members.DefaultIfEmpty().
                Aggregate((result, mem) => result + ", \n \t" + mem) + ";\n").
                Aggregate(string.Concat);
            }

            yield return null;
        }

    }



}
