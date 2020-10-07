// Copyright 2019 The Zcoin developers
// Copyright 2020 We Develop Co.,Ltd.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
namespace WeDev.Extensions.Threading.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public sealed class ShutdownGuardTests : IDisposable
    {
        private readonly List<TaskCompletionSource<bool>> ready;
        private readonly List<Thread> threads;
        private readonly ShutdownGuard subject;

        public ShutdownGuardTests()
        {
            this.ready = new List<TaskCompletionSource<bool>>();
            this.threads = new List<Thread>();

            for (var i = 0; i < Environment.ProcessorCount; i++)
            {
                this.ready.Add(new TaskCompletionSource<bool>());
                this.threads.Add(new Thread(this.SeparatedThread));
            }

            this.subject = new ShutdownGuard();
        }

        public void Dispose()
        {
            foreach (var thread in this.threads.Where(t => t.IsAlive))
            {
                this.ShutdownThread(thread);
            }

            this.subject.Dispose();
        }

        [Fact]
        public async Task PenetrateAsync_WhileOtherThreadsRepeatedTryAcquire_ShouldSuccess()
        {
            // Arrange.
            for (var i = 0; i < this.threads.Count; i++)
            {
                this.threads[i].Start(this.ready[i]);
            }

            await Task.WhenAll(this.ready.Select(r => r.Task));
            await Task.Yield();

            Assert.All(this.threads, t => Assert.True(t.IsAlive));

            // Act.
            await this.subject.PenetrateAsync();

            // Assert.
            Assert.All(this.threads, t => Assert.True(t.Join(5000)));
        }

        private void SeparatedThread(object? obj)
        {
            var ready = (TaskCompletionSource<bool>?)obj;

            for (;;)
            {
                if (!this.subject.TryAcquire())
                {
                    break;
                }

                if (ready != null)
                {
                    ready.SetResult(true);
                    ready = null;
                }

                this.subject.Release();
            }
        }

        private void ShutdownThread(Thread thread)
        {
            for (;;)
            {
                try
                {
                    thread.Join();
                }
                catch (ThreadStateException)
                {
                    break;
                }
                catch (ThreadInterruptedException)
                {
                    continue;
                }

                break;
            }
        }
    }
}
