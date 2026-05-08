// Assets/Scripts/Data/WorkerDatabase.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject database chứa danh sách tất cả worker types.
/// Tạo asset: Right-click > Create > TCGShop > Data > Worker Database
/// </summary>
[CreateAssetMenu(
    fileName = "WorkerDatabase",
    menuName = "TCGShop/Data/Worker Database",
    order = 7
)]
public class WorkerDatabase : ScriptableObject
{
    [Header("Workers")]
    public List<WorkerDefinition> workers = new List<WorkerDefinition>();

    public WorkerDefinition GetWorker(string workerId)
    {
        return workers.Find(w => w.workerId == workerId);
    }

    public List<WorkerDefinition> GetAvailableWorkers(int currentShopLevel)
    {
        return workers.FindAll(w => w.requiredShopLevel <= currentShopLevel);
    }

    public void CreateDefaultDatabase()
    {
        workers.Clear();

        workers.Add(new WorkerDefinition
        {
            workerId = "worker_slow",
            displayName = "Junior Cashier",
            description = "A rookie who takes their time. Good for beginners.",
            requiredShopLevel = 1,
            hiringFee = 100f,
            dailySalary = 20f,
            checkoutSpeed = CheckoutSpeed.Slow,
            speedDescription = "5s per customer"
        });

        workers.Add(new WorkerDefinition
        {
            workerId = "worker_normal",
            displayName = "Standard Cashier",
            description = "Reliable and efficient.",
            requiredShopLevel = 5,
            hiringFee = 200f,
            dailySalary = 40f,
            checkoutSpeed = CheckoutSpeed.Normal,
            speedDescription = "3s per customer"
        });

        workers.Add(new WorkerDefinition
        {
            workerId = "worker_fast",
            displayName = "Senior Cashier",
            description = "Fast hands, happy customers.",
            requiredShopLevel = 10,
            hiringFee = 400f,
            dailySalary = 80f,
            checkoutSpeed = CheckoutSpeed.Fast,
            speedDescription = "1.5s per customer"
        });

        workers.Add(new WorkerDefinition
        {
            workerId = "worker_veryfast",
            displayName = "Elite Cashier",
            description = "Lightning fast. The best in the business.",
            requiredShopLevel = 20,
            hiringFee = 800f,
            dailySalary = 160f,
            checkoutSpeed = CheckoutSpeed.VeryFast,
            speedDescription = "0.8s per customer"
        });
    }
}
