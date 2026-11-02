using UnityEngine;
public class CollectibleRotate : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private char rotationAxis = 'y';

    private void Update()
    {
        if (rotationAxis == 'y')
            transform.Rotate(
            0f,
            rotationSpeed * Time.deltaTime,
            0f
            );
        else if (rotationAxis == 'x')
            transform.Rotate(
            rotationSpeed * Time.deltaTime,
            0f,
            0f
            );
        else
        transform.Rotate(
            0f,
            0f,
            rotationSpeed * Time.deltaTime
            );
    }
}