using System;
using System.Collections.Generic;

namespace HttpScript.Parsing
{
    internal class BufferedQueue<T>
    {
        private T[] items;
        private int dequeueIndex = 0;
        private int addIndex = 0;

        // allow users to jump back in time as far as dequeueing
        // is concerned, but preserve added items.
        // this allows the parser to backtrack without resetting the
        // work that the lexer has already done.
        private Stack<int> restorePoints = new();

        public int Count => this.addIndex - this.dequeueIndex;

        public BufferedQueue() : this(capacity: 64)
        {
        }

        public BufferedQueue(int capacity)
        {
            this.items = new T[capacity];
        }

        public void PushRestorePoint()
        {
            this.restorePoints.Push(this.dequeueIndex);
        }

        public void PopRestorePoint()
        {
            this.dequeueIndex = this.restorePoints.Pop();
        }

        public void DiscardRestorePoint()
        {
            _ = this.restorePoints.Pop();
        }

        public void Clear()
        {
            // we want to keep the items we have already dequeued,
            // but any future items should be discarded
            this.addIndex = this.dequeueIndex;
            this.restorePoints.Clear();
        }

        public IEnumerable<T> GetDequeuedItems() => new ArraySegment<T>(this.items, 0, this.dequeueIndex);

        public void Enqueue(T token)
        {
            var newIndex = this.addIndex;

            this.EnsureCapacity(newIndex + 1);

            this.items[newIndex] = token;

            this.addIndex += 1;
        }

        public bool TryDequeue(out T item)
        {
            if (this.TryPeek(out item))
            {
                this.dequeueIndex += 1;
                return true;
            }

            return false;
        }

        public bool TryPeek(out T item)
        {
            var index = this.dequeueIndex;

            if (index >= this.addIndex)
            {
                item = default!; // contract: should not read this if we returned false
                return false;
            }

            item = this.items[index];
            return true;
        }

        private void EnsureCapacity(int min)
        {
            var curLength = this.items.Length;

            if (curLength < min)
            {
                var newArr = new T[curLength * 2];
                Array.Copy(this.items, newArr, curLength);
                this.items = newArr;
            }
        }

    }
}
