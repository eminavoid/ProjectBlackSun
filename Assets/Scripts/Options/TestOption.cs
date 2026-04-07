using UnityEngine;

[CreateAssetMenu(fileName = "Test Option", menuName = "Options/Test Option", order = 1)]
public class TestOption : Option
{
    public override void ExecuteOption()
    {
        Debug.Log("option selected");
    }
}