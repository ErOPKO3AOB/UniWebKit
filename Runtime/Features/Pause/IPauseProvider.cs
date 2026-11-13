using System;

namespace UniWebKit
{
    public interface IPauseProvider : IUniWebFeature, IDisposable
    {
        bool IsPaused { get; }
        event Action<bool> PauseChanged;
        // Changes only the game's own reason for pausing; never clears a platform pause.
        void SetPaused(bool paused);
    }
}
