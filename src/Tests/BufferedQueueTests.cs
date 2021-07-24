using HttpScript.Parsing;
using Xunit;

namespace Tests
{
    public class BufferedQueueTests
    {
        [Fact]
        public void CanRestoreToPreviousState()
        {
            var queue = new BufferedQueue<int>();

            queue.Enqueue(10);
            queue.Enqueue(20);

            var success = queue.TryDequeue(out var item);

            Assert.True(success);
            Assert.Equal(10, item);

            queue.PushRestorePoint();

            success = queue.TryDequeue(out item);

            Assert.True(success);
            Assert.Equal(20, item);

            queue.Enqueue(30);

            queue.PopRestorePoint();

            success = queue.TryDequeue(out item);

            Assert.True(success);
            Assert.Equal(20, item);
            
            success = queue.TryDequeue(out item);

            Assert.True(success);
            Assert.Equal(30, item);
        }

        [Fact]
        public void CanEnqueueManyItems()
        {
            const int itemCount = 10000;

            var queue = new BufferedQueue<int>();

            for (int i = 0; i < itemCount; i++)
            {
                queue.Enqueue(i);
            }

            for (int i = 0; i < itemCount; i++)
            {
                var peekResult = queue.TryPeek(out var peekVal);
                var result = queue.TryDequeue(out var val);

                Assert.True(peekResult);
                Assert.True(result);
                Assert.Equal(i, peekVal);
                Assert.Equal(i, val);
            }

            var finalPeekResult = queue.TryPeek(out var _);
            var finalResult = queue.TryDequeue(out var _);

            Assert.False(finalPeekResult);
            Assert.False(finalResult);

            int index = 0;
            foreach (var item in queue.GetDequeuedItems())
            {
                Assert.Equal(index, item);

                index += 1;
            }
        }
    }
}
