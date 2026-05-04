// Assets/Scripts/Debug/Step4TestRunner.cs

using UnityEngine;

/// <summary>
/// Test runner tạm thời cho Step 4.
/// XÓA SAU KHI TEST THÀNH CÔNG.
/// </summary>
public class Step4TestRunner : MonoBehaviour
{
    [SerializeField] private CustomerAIController testNPC;

    private void Start()
    {
        if (testNPC != null)
            testNPC.Initialize("npc_test_001", CustomerAIController.CustomerIntent.Buy);
    }
}
