using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class fuckery : MonoBehaviour {

	// Use this for initialization
	void Start () {
        nthChild(7,transform).SetParent(transform);
        print(nthChild(7, transform).parent == nthChild(6,transform));
	}
	

    Transform nthChild(int n,Transform t)
    {
        if (n == 0 || t.childCount == 0) return t;
        else return t.GetChild(0);
    }
}
