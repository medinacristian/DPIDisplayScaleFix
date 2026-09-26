namespace DPIDisplayScaleFix.Core;

public sealed class StableTopologyTracker
{
    private readonly TimeSpan _requiredStablePeriod;
    private readonly TimeProvider _timeProvider;
    private string _stableTopology;
    private string? _candidateTopology;
    private long _candidateSince;

    public StableTopologyTracker(
        string initialTopology,
        TimeSpan requiredStablePeriod,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(initialTopology);
        if (requiredStablePeriod < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(requiredStablePeriod));

        _stableTopology = initialTopology;
        _requiredStablePeriod = requiredStablePeriod;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool Observe(string topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        if (topology == _stableTopology)
        {
            ResetCandidate();
            return false;
        }

        if (topology != _candidateTopology)
        {
            _candidateTopology = topology;
            _candidateSince = _timeProvider.GetTimestamp();
            return false;
        }

        if (_timeProvider.GetElapsedTime(_candidateSince) < _requiredStablePeriod)
            return false;

        _stableTopology = topology;
        ResetCandidate();
        return true;
    }

    public void ResetCandidate() => _candidateTopology = null;
}