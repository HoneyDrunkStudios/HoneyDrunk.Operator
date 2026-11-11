using System.Collections;

namespace HoneyDrunk.Operator.Abstractions.Tests.Approvals;

/// <summary>A caller-owned collection that changes its values between enumerations.</summary>
internal sealed class ChangingSubjects(int yieldedCount = 1) : IReadOnlyCollection<string>
{
    public int Count => 1;

    public int Enumerations { get; private set; }

    public int Yielded { get; private set; }

    public IEnumerator<string> GetEnumerator()
    {
        this.Enumerations++;
        for (var index = 0; index < yieldedCount; index++)
        {
            this.Yielded++;
            yield return this.Enumerations == 1 ? "agent:a" : " ";
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
