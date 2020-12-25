using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace YoutubeDLWrapper.Tests
{
    public static class TestUtils
    {
        public static void DeployEmbeddedResource(string filename, string deployDir)
        {
            var assembly = typeof(TestUtils).Assembly;
            using var stream = ResourceLoader.Current.GetEmbeddedResourceStream(assembly, filename);
            using var streamOut = File.OpenWrite(Path.Combine(deployDir, filename));
            stream.CopyTo(streamOut);
        }
    }
}
