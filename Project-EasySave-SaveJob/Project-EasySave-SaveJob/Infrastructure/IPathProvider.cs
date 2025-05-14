using System;
using System.Collections.Generic;
using System.Text;

namespace Projet.Infrastructure
{
    public interface IPathProvider
    {
        string GetBaseDir();
        string GetLogDir();
        string GetStatusDir();
    }
}
