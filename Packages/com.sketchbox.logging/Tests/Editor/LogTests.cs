using System;
using System.Collections.Generic;
using NUnit.Framework;
using Sketchbox.Logging;
using Object = UnityEngine.Object;

namespace Sketchbox.Logging.Tests
{
    /// <summary>
    /// Tests the logging facade's decision logic: severity filtering, channel tagging,
    /// sink routing and format-failure handling.
    ///
    /// Every test installs a recording sink first. Without it the default sink would
    /// write to the Unity console, and a logged error fails the test run.
    /// </summary>
    public class LogTests
    {
        private sealed class RecordingSink : ILogSink
        {
            public readonly List<string> Lines = new List<string>();
            public readonly List<LogLevel> Levels = new List<LogLevel>();
            public readonly List<Exception> Exceptions = new List<Exception>();

            public void Write(LogLevel level, string channel, string message, Object context)
            {
                Levels.Add(level);
                Lines.Add(string.IsNullOrEmpty(channel) ? message : "[" + channel + "] " + message);
            }

            public void WriteException(Exception exception, Object context)
            {
                Exceptions.Add(exception);
            }
        }

        private RecordingSink _sink;
        private LogLevel _originalLevel;

        [SetUp]
        public void SetUp()
        {
            _originalLevel = Log.MinimumLevel;
            _sink = new RecordingSink();
            Log.SetSink(_sink);
            Log.MinimumLevel = LogLevel.Debug;
        }

        [TearDown]
        public void TearDown()
        {
            Log.MinimumLevel = _originalLevel;
            Log.SetSink(null);
        }

        [Test]
        public void Info_ForwardsMessageToSink()
        {
            Log.Info("hello");

            Assert.AreEqual(1, _sink.Lines.Count);
            Assert.AreEqual("hello", _sink.Lines[0]);
            Assert.AreEqual(LogLevel.Info, _sink.Levels[0]);
        }

        [Test]
        public void Severities_MapToDistinctLevels()
        {
            Log.Info("i");
            Log.Warn("w");
            Log.Error("e");

            CollectionAssert.AreEqual(
                new[] { LogLevel.Info, LogLevel.Warn, LogLevel.Error }, _sink.Levels);
        }

        [Test]
        public void MinimumLevel_DiscardsLowerSeverities()
        {
            Log.MinimumLevel = LogLevel.Error;

            Log.Info("dropped");
            Log.Warn("dropped");
            Log.Error("kept");

            Assert.AreEqual(1, _sink.Lines.Count);
            Assert.AreEqual("kept", _sink.Lines[0]);
        }

        [Test]
        public void MinimumLevelOff_DiscardsEverything()
        {
            Log.MinimumLevel = LogLevel.Off;

            Log.Error("dropped");
            Log.Exception(new InvalidOperationException("dropped"));

            Assert.AreEqual(0, _sink.Lines.Count);
            Assert.AreEqual(0, _sink.Exceptions.Count);
        }

        [Test]
        public void IsEnabled_MatchesMinimumLevel()
        {
            Log.MinimumLevel = LogLevel.Warn;

            Assert.IsFalse(Log.IsEnabled(LogLevel.Info));
            Assert.IsTrue(Log.IsEnabled(LogLevel.Warn));
            Assert.IsTrue(Log.IsEnabled(LogLevel.Error));
        }

        [Test]
        public void NullMessage_DoesNotThrow()
        {
            Assert.DoesNotThrow(delegate { Log.Info(null); });
            Assert.AreEqual("null", _sink.Lines[0]);
        }

        [Test]
        public void InfoFormat_FormatsArguments()
        {
            Log.InfoFormat("{0} of {1}", 2, 5);

            Assert.AreEqual("2 of 5", _sink.Lines[0]);
        }

        [Test]
        public void MalformedFormat_FallsBackToRawFormatString()
        {
            // A bad format string in a log call must not throw at the call site.
            Assert.DoesNotThrow(delegate { Log.InfoFormat("{0} {1}", "only-one"); });
            Assert.AreEqual("{0} {1}", _sink.Lines[0]);
        }

        [Test]
        public void Channel_PrefixesMessages()
        {
            Log.Channel("net").Info("connected");

            Assert.AreEqual("[net] connected", _sink.Lines[0]);
        }

        [Test]
        public void Exception_ForwardsToSink()
        {
            Exception thrown = new InvalidOperationException("boom");

            Log.Exception(thrown);

            Assert.AreEqual(1, _sink.Exceptions.Count);
            Assert.AreSame(thrown, _sink.Exceptions[0]);
        }

        [Test]
        public void NullException_ReportsInsteadOfThrowing()
        {
            Assert.DoesNotThrow(delegate { Log.Exception(null); });

            Assert.AreEqual(0, _sink.Exceptions.Count);
            Assert.AreEqual(1, _sink.Lines.Count);
            Assert.AreEqual(LogLevel.Error, _sink.Levels[0]);
        }

        [Test]
        public void SetSinkNull_RestoresDefaultSinkWithoutThrowing()
        {
            Log.SetSink(null);

            // The default sink writes to the Unity console; only assert it is installed
            // and that the recording sink stops receiving.
            Log.MinimumLevel = LogLevel.Off;
            Assert.DoesNotThrow(delegate { Log.Info("goes nowhere"); });
            Assert.AreEqual(0, _sink.Lines.Count);
        }
    }
}
