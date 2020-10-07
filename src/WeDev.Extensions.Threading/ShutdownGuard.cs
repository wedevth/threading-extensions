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
namespace WeDev.Extensions.Threading
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class ShutdownGuard : IDisposable
    {
        private readonly SemaphoreSlim penetrated;
        private volatile bool penetrating;
        private volatile int acquired;
        private bool disposed;

        public ShutdownGuard()
        {
            this.penetrated = new SemaphoreSlim(0);
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            if (this.acquired > 0)
            {
                throw new InvalidOperationException("The object is still acquired.");
            }

            this.penetrated.Dispose();

            this.disposed = true;
        }

        public bool TryAcquire()
        {
            if (this.penetrating)
            {
                return false;
            }

            return Interlocked.Increment(ref this.acquired) > 0;
        }

        public int Release()
        {
            var current = Interlocked.Decrement(ref this.acquired);

            if (current < 0)
            {
                throw new InvalidOperationException("The object is not acquired.");
            }

            if (this.penetrating && current == 0)
            {
                this.penetrated.Release();
            }

            return current;
        }

        public async Task PenetrateAsync(CancellationToken cancellationToken = default)
        {
            this.penetrating = true;

            while (Interlocked.CompareExchange(ref this.acquired, int.MinValue, 0) != 0)
            {
                await this.penetrated.WaitAsync(cancellationToken);
            }
        }
    }
}
