using System;
using System.Collections.Generic;

public class PriorityQueue<T>
{
    private List<T> data;
    private Func<T, float> priorityFunction;

    public int Count => data.Count;

    public PriorityQueue(Func<T, float> priorityFunc)
    {
        this.data = new List<T>();
        this.priorityFunction = priorityFunc;
    }

    public void Enqueue(T item)
    {
        data.Add(item);
        int childIndex = data.Count - 1;

        while (childIndex > 0)
        {
            int parentIndex = (childIndex - 1) / 2;

            if (priorityFunction(data[childIndex]) >= priorityFunction(data[parentIndex]))
                break;

            // Swap
            T tmp = data[childIndex];
            data[childIndex] = data[parentIndex];
            data[parentIndex] = tmp;

            childIndex = parentIndex;
        }
    }

    public T Dequeue()
    {
        int lastIndex = data.Count - 1;
        T frontItem = data[0];

        data[0] = data[lastIndex];
        data.RemoveAt(lastIndex);

        lastIndex--;

        if (lastIndex > 0)
        {
            int parentIndex = 0;

            while (true)
            {
                int leftChildIndex = parentIndex * 2 + 1;
                if (leftChildIndex > lastIndex)
                    break;

                int rightChildIndex = leftChildIndex + 1;
                int minIndex = leftChildIndex;

                if (rightChildIndex <= lastIndex &&
                    priorityFunction(data[rightChildIndex]) < priorityFunction(data[leftChildIndex]))
                {
                    minIndex = rightChildIndex;
                }

                if (priorityFunction(data[parentIndex]) <= priorityFunction(data[minIndex]))
                    break;

                // Swap
                T tmp = data[parentIndex];
                data[parentIndex] = data[minIndex];
                data[minIndex] = tmp;

                parentIndex = minIndex;
            }
        }

        return frontItem;
    }

    public T Peek()
    {
        return data[0];
    }

    public bool Contains(T item)
    {
        return data.Contains(item);
    }
}