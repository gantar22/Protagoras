using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class test_coroutines : MonoBehaviour {

	// Use this for initialization
	void Start () {
        StartCoroutine(test1());
        StartCoroutine(test2()); //this works and is not the problem I am having
	}
	
	IEnumerator test1()
    {
        while(true)
        {
            print("test1 end");
            yield return new WaitForSeconds(1);
            print("test1 start");
        }
    }

    IEnumerator test2()
    {
        int i = 0;
        while(true)
        {
            i++;
            print("test2 end");
            if(i % 2 == 0) yield return null;
            print("test2 start");
        }
    }
}
