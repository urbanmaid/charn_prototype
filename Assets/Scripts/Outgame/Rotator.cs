using UnityEngine;

public class Rotator : MonoBehaviour
{
    [SerializeField] Vector3 axis = new Vector3(0, 0, 1f);
    [SerializeField] float speed = 32.0f;
	[SerializeField] bool isRotating = true;
	
    // Update is called once per frame
	void FixedUpdate () 
    {
        if (isRotating) 
        {
            // Rotate the object around its local coordinate system
            //transform.Rotate(axis, speed * Time.deltaTime);
            transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
            // Rotate the object around its world coordinate system
            //transform.Rotate(axis, speed * Time.deltaTime, Space.World);
        }
    }
}
