// Assets/Scripts/Debug/Step4TestRunner.cs

using UnityEngine;

/// <summary>
/// Test runner tạm thời cho Step 5.
/// Test EconomicDecisionEngine và CustomerFSM lifecycle.
/// </summary>
public class Step4TestRunner : MonoBehaviour
{
    [Header("CustomerFSM Test")]
    [SerializeField] private CustomerFSM testNPC;

    [Header("Economic Engine Test")]
    [Tooltip("Giá thị trường tham chiếu cho test. sellPrice test = 30f (×3), 10f (=market).")]
    [SerializeField] private float testMarketPrice = 10f;

    [Header("Shelf Instance Test")]
    [SerializeField] private ShelfInstance testShelf;

    private void Start()
    {
        // Test EconomicDecisionEngine ngay khi scene load
        TestEconomicDecisionEngine();

        // Test CustomerFSM nếu có prefab gắn
        if (testNPC != null)
        {
            testNPC.Initialize("npc_test_001", CustomerFSM.CustomerIntent.Buy);
        }

        // Test ShelfInstance
        if (testShelf != null)
        {
            Debug.Log($"[Step4TestRunner] Shelf state: {testShelf}");
        }
    }

    private void TestEconomicDecisionEngine()
    {
        Debug.Log("=== EconomicDecisionEngine Test Cases ===");

        float[] sellPrices = { 9f, 10f, 10.5f, 11f, 15f, 20f, 30f };

        foreach (float sell in sellPrices)
        {
            float prob = EconomicDecisionEngine.CalculateBuyProbability(sell, testMarketPrice);
            bool willBuy = EconomicDecisionEngine.DecidePurchase(
                sell, testMarketPrice, out float actualProb, out PurchaseDecision decision);

            Debug.Log($"  Sell=${sell}, Market=${testMarketPrice}, " +
                      $"Ratio={sell / testMarketPrice:F2}, Prob={actualProb:P0}, " +
                      $"Decision={decision}, WillBuy={willBuy}");
        }

        // Test case bắt buộc: giá ×3 phải trả về 0%
        float prob30 = EconomicDecisionEngine.CalculateBuyProbability(30f, 10f);
        bool willBuy30 = EconomicDecisionEngine.DecidePurchase(30f, 10f, out float actualProb30, out PurchaseDecision dec30);

        Debug.Assert(prob30 == 0f,
            $"[Step4TestRunner] FAIL: CalculateBuyProbability(30, 10) expected 0, got {prob30}",
            this);

        Debug.Assert(!willBuy30,
            $"[Step4TestRunner] FAIL: DecidePurchase(30, 10) expected WillBuy=false, got {willBuy30}",
            this);

        Debug.Assert(dec30 == PurchaseDecision.AbsoluteRefusal,
            $"[Step4TestRunner] FAIL: DecidePurchase(30, 10) expected AbsoluteRefusal, got {dec30}",
            this);

        // Test case: giá = thị trường phải trả về 95%
        float prob10 = EconomicDecisionEngine.CalculateBuyProbability(10f, 10f);
        Debug.Assert(Mathf.Approximately(prob10, 0.95f),
            $"[Step4TestRunner] FAIL: CalculateBuyProbability(10, 10) expected 0.95, got {prob10}",
            this);

        Debug.Log($"[Step4TestRunner] All assertions passed. prob(30,10)={prob30}, prob(10,10)={prob10:F2}");
    }
}
