using System;
using System.Threading;

namespace Projet.Infrastructure
{
    public class SingleInstanceManager : IDisposable
    {
        private Mutex _mutex;
        private const string MutexName = "EasySave_SingleInstance_Mutex";
        private bool _hasHandle = false;

        public SingleInstanceManager()
        {
            try
            {
                bool createdNew;
                _mutex = new Mutex(true, MutexName, out createdNew);
                _hasHandle = createdNew;

                if (!_hasHandle)
                {
                    try
                    {
                        // Essayez d'acquérir le mutex si une autre instance l'a abandonné
                        _hasHandle = _mutex.WaitOne(TimeSpan.Zero, true);
                    }
                    catch (AbandonedMutexException)
                    {
                        // Le mutex a été abandonné par une autre instance qui s'est terminée de façon inattendue
                        _hasHandle = true;
                    }
                }
            }
            catch (Exception)
            {
                _hasHandle = false;
                if (_mutex != null)
                {
                    _mutex.Close();
                    _mutex = null;
                }
            }
        }

        public bool IsFirstInstance()
        {
            return _hasHandle;
        }

        public void Dispose()
        {
            if (_mutex != null)
            {
                if (_hasHandle)
                {
                    try
                    {
                        _mutex.ReleaseMutex();
                    }
                    catch (Exception)
                    {
                        // Ignore les erreurs lors de la libération
                    }
                }
                _mutex.Close();
                _mutex = null;
            }
        }
    }
} 