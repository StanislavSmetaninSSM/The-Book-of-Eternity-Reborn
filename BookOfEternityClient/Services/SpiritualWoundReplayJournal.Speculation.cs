namespace BookOfEternityClient.Services;

internal sealed partial class SpiritualWoundReplayJournal
{
    private Speculation? _activeSpeculation;

    /// <summary>
    /// Rejects closing a capture transaction while its allocation callback is still active.
    /// </summary>
    private void EnsureScopeCanClose()
    {
        if (!_requireScope || !_requesting) return;
        Invalidate();
        throw new InvalidOperationException("An allocation callback cannot close its scope.");
    }

    /// <summary>
    /// Starts one serialized speculative allocation scope without granting execution authority.
    /// Callers using a projection clock must enter through its combined scope instead.
    /// </summary>
    /// <returns>
    /// A single-use scope that restores healthy uncommitted allocations when disposed.
    /// </returns>
    internal Speculation BeginSpeculation()
    {
        EnsureUsable();
        if (_activeSpeculation is not null)
        {
            Invalidate();
            throw new InvalidOperationException("An allocation speculation is already active.");
        }
        return _activeSpeculation = new Speculation(this);
    }

    /// <summary>
    /// Retains private cursor and index state for one serialized speculative allocation attempt.
    /// This token cannot restore a permanently faulted journal or undo gameplay execution.
    /// </summary>
    internal sealed class Speculation : IDisposable
    {
        private readonly SpiritualWoundReplayJournal _owner;
        private readonly int _position;
        private readonly int _rowCount;
        private readonly HashSet<(string Kind, string Owner, string Coordinate)> _keys;
        private readonly HashSet<string> _identities;
        private bool _closed;

        /// <summary>
        /// Snapshots all journal indexes, including unconsumed retained rows.
        /// </summary>
        /// <param name="owner">
        /// Journal that registers this token as its sole active speculation.
        /// </param>
        internal Speculation(SpiritualWoundReplayJournal owner)
        {
            _owner = owner;
            _position = owner._position;
            _rowCount = owner._rows.Count;
            _keys = new(owner._keys);
            _identities = new(owner._identities, StringComparer.Ordinal);
        }

        /// <summary>
        /// Retains speculative rows after the caller's owner transition has accepted them.
        /// Reusing a token or committing a faulted journal fails closed.
        /// </summary>
        internal void Commit()
        {
            _owner.EnsureUsable();
            _owner.EnsureScopeCanClose();
            if (_closed || !ReferenceEquals(_owner._activeSpeculation, this))
            {
                _owner.Invalidate();
                throw new InvalidOperationException("The allocation speculation is stale or foreign.");
            }
            _closed = true;
            _owner._activeSpeculation = null;
        }

        /// <summary>
        /// Restores an uncommitted healthy scope; repeated or committed cleanup is harmless.
        /// Faulted journals remain unusable and are never revived by cleanup.
        /// </summary>
        public void Dispose()
        {
            if (_closed) return;
            _owner.EnsureScopeCanClose();
            _closed = true;
            if (!ReferenceEquals(_owner._activeSpeculation, this))
            {
                _owner.Invalidate();
                throw new InvalidOperationException("The allocation speculation is not current.");
            }
            _owner._activeSpeculation = null;
            if (_owner._faulted) return;
            while (_owner._rows.Count > _rowCount) _owner._rows.RemoveAt(_owner._rows.Count - 1);
            _owner._position = _position;
            _owner._keys.Clear();
            _owner._keys.UnionWith(_keys);
            _owner._identities.Clear();
            _owner._identities.UnionWith(_identities);
        }
    }
}
