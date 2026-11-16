using System;
using System.IO;
using NUnit.Framework;
namespace RemixTable.Tests
{
    // NUnit fixture setup runs inside Play Mode, after Unity's domain reload. Scene tests
    // never read or write user remixes, and each test starts with its own save folder.
    public abstract class RemixSavedSceneTest
    {
        Func<RemixSaveStore> previous;
        [SetUp] public void IsolateRemixSaves()
        {
            previous=RemixSaveStore.DefaultFactory;
            string folder=Path.Combine(Path.GetTempPath(),"remix-play-tests",Guid.NewGuid().ToString("N"));
            RemixSaveStore.DefaultFactory=()=>new RemixSaveStore(folder);
        }
        [TearDown] public void RestoreRemixSaveFactory() => RemixSaveStore.DefaultFactory=previous;
    }
}
