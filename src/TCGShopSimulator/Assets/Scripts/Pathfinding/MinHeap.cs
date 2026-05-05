// Assets/Scripts/Pathfinding/MinHeap.cs

using System;
using System.Collections.Generic;

/// <summary>
/// Binary Min-Heap tổng quát dùng cho A* Open List.
///
/// TẠI SAO MIN-HEAP:
///   A* cần liên tục lấy node có FCost nhỏ nhất từ Open List.
///   List + sort: O(n log n) mỗi lần extract → chậm với nhiều node.
///   MinHeap: Insert O(log n), ExtractMin O(log n) → tối ưu.
///
/// BINARY HEAP INVARIANT:
///   Với mọi node tại index i:
///     Parent(i) = (i-1)/2
///     LeftChild(i) = 2*i + 1
///     RightChild(i) = 2*i + 2
///   Luôn đảm bảo: heap[parent] <= heap[children]
///   → Phần tử nhỏ nhất luôn ở heap[0]
///
/// INTERFACE IHeapItem:
///   T phải implement IHeapItem để heap lưu HeapIndex.
///   HeapIndex cho phép UpdateItem() O(log n) thay vì O(n) search.
/// </summary>
public class MinHeap<T> where T : IComparable<T>, IHeapItem
{
    private readonly List<T> _items;

    // =========================================================================
    // CONSTRUCTORS
    // =========================================================================

    public MinHeap(int initialCapacity = 64)
    {
        _items = new List<T>(initialCapacity);
    }

    // =========================================================================
    // PROPERTIES
    // =========================================================================

    public int Count => _items.Count;
    public bool IsEmpty => _items.Count == 0;

    /// <summary>Phần tử nhỏ nhất (gốc heap) — O(1).</summary>
    public T Min => _items.Count > 0 ? _items[0] : default;

    // =========================================================================
    // CORE OPERATIONS
    // =========================================================================

    /// <summary>
    /// Thêm phần tử vào heap. O(log n).
    ///
    /// THUẬT TOÁN:
    ///   1. Thêm vào cuối array
    ///   2. SiftUp: so sánh với parent, swap nếu nhỏ hơn
    ///   3. Lặp đến khi heap invariant thỏa mãn
    /// </summary>
    public void Insert(T item)
    {
        _items.Add(item);
        int index = _items.Count - 1;
        item.HeapIndex = index;
        SiftUp(index);
    }

    /// <summary>
    /// Lấy và xóa phần tử nhỏ nhất (gốc). O(log n).
    ///
    /// THUẬT TOÁN:
    ///   1. Lưu root (phần tử min)
    ///   2. Đưa phần tử cuối lên root
    ///   3. Xóa phần tử cuối
    ///   4. SiftDown: so sánh root với children, swap với child nhỏ hơn
    ///   5. Lặp đến khi heap invariant thỏa mãn
    /// </summary>
    public T ExtractMin()
    {
        if (IsEmpty)
        {
            Debug.LogError("[MinHeap] Attempted ExtractMin on an empty heap!");
            return default;
        }

        T min = _items[0];
        int lastIndex = _items.Count - 1;

        // Đưa phần tử cuối lên root
        _items[0] = _items[lastIndex];
        _items[0].HeapIndex = 0;

        // Xóa phần tử cuối
        _items.RemoveAt(lastIndex);

        // Restore heap property
        if (!IsEmpty)
            SiftDown(0);

        min.HeapIndex = -1;
        return min;
    }

    /// <summary>
    /// Cập nhật priority của một item đã có trong heap. O(log n).
    /// Dùng khi tìm được G cost tốt hơn cho một node đã trong Open List.
    /// Nhờ HeapIndex, không cần tìm kiếm O(n) — chỉ cần SiftUp từ vị trí đã biết.
    /// </summary>
    public void UpdateItem(T item)
    {
        if (item.HeapIndex < 0 || item.HeapIndex >= _items.Count) return;
        SiftUp(item.HeapIndex);
    }

    /// <summary>Kiểm tra item có trong heap không — O(1) nhờ HeapIndex.</summary>
    public bool Contains(T item) =>
        item.HeapIndex >= 0 && item.HeapIndex < _items.Count && _items[item.HeapIndex].Equals(item);

    /// <summary>Xóa toàn bộ heap.</summary>
    public void Clear()
    {
        foreach (var item in _items)
            item.HeapIndex = -1;
        _items.Clear();
    }

    // =========================================================================
    // SIFT OPERATIONS — Duy trì heap invariant
    // =========================================================================

    /// <summary>
    /// SiftUp: Đẩy phần tử tại index lên đúng vị trí.
    /// Dùng sau Insert (phần tử mới ở cuối).
    ///
    /// THUẬT TOÁN:
    ///   Trong khi index > 0 VÀ item < parent:
    ///     Swap(item, parent)
    ///     index = parentIndex
    /// </summary>
    private void SiftUp(int index)
    {
        while (index > 0)
        {
            int parentIndex = (index - 1) / 2;

            // Nếu item >= parent → heap invariant thỏa mãn, dừng
            if (_items[index].CompareTo(_items[parentIndex]) >= 0)
                break;

            Swap(index, parentIndex);
            index = parentIndex;
        }
    }

    /// <summary>
    /// SiftDown: Đẩy phần tử tại index xuống đúng vị trí.
    /// Dùng sau ExtractMin (phần tử cuối được đưa lên root).
    ///
    /// THUẬT TOÁN:
    ///   Trong khi có ít nhất một child:
    ///     Tìm child nhỏ nhất
    ///     Nếu item <= child nhỏ nhất → dừng
    ///     Swap(item, child nhỏ nhất)
    ///     index = childIndex
    /// </summary>
    private void SiftDown(int index)
    {
        int count = _items.Count;

        while (true)
        {
            int leftChild = 2 * index + 1;
            int rightChild = 2 * index + 2;
            int smallest = index;

            // Tìm child nhỏ nhất
            if (leftChild < count && _items[leftChild].CompareTo(_items[smallest]) < 0)
                smallest = leftChild;

            if (rightChild < count && _items[rightChild].CompareTo(_items[smallest]) < 0)
                smallest = rightChild;

            // Nếu đã là nhỏ nhất trong nhóm → dừng
            if (smallest == index) break;

            Swap(index, smallest);
            index = smallest;
        }
    }

    private void Swap(int indexA, int indexB)
    {
        T temp = _items[indexA];
        _items[indexA] = _items[indexB];
        _items[indexB] = temp;

        // Cập nhật HeapIndex cho cả hai
        _items[indexA].HeapIndex = indexA;
        _items[indexB].HeapIndex = indexB;
    }
}

/// <summary>
/// Interface bắt buộc cho các item trong MinHeap.
/// HeapIndex cho phép UpdateItem O(log n) không cần tìm kiếm.
/// </summary>
public interface IHeapItem
{
    int HeapIndex { get; set; }
}
