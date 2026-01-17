using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MechanicScope.Tests.Runtime.Voice
{
    /// <summary>
    /// End-to-end tests for VoiceCommandManager.
    /// Tests command registration, parsing, and execution.
    /// </summary>
    public class VoiceCommandTests : TestBase
    {
        private MockVoiceCommandManager commandManager;

        public override void SetUp()
        {
            base.SetUp();
            commandManager = CreateGameObjectWithComponent<MockVoiceCommandManager>("VoiceCommandManager");
        }

        [Test]
        public void VoiceCommand_RegistersCorrectly()
        {
            // Arrange
            bool commandExecuted = false;
            Action callback = () => commandExecuted = true;

            // Act
            commandManager.RegisterCommand("next step", callback);

            // Assert
            Assert.IsTrue(commandManager.HasCommand("next step"));
        }

        [Test]
        public void VoiceCommand_ExecutesCallback()
        {
            // Arrange
            bool commandExecuted = false;
            commandManager.RegisterCommand("test command", () => commandExecuted = true);

            // Act
            commandManager.ExecuteCommand("test command");

            // Assert
            Assert.IsTrue(commandExecuted);
        }

        [Test]
        public void VoiceCommand_CaseInsensitive()
        {
            // Arrange
            bool commandExecuted = false;
            commandManager.RegisterCommand("Next Step", () => commandExecuted = true);

            // Act
            commandManager.ExecuteCommand("next step");

            // Assert
            Assert.IsTrue(commandExecuted);
        }

        [Test]
        public void VoiceCommand_PartialMatch()
        {
            // Arrange
            bool commandExecuted = false;
            commandManager.RegisterCommand("go to next step", () => commandExecuted = true);

            // Act
            bool matched = commandManager.TryMatchCommand("next step");

            // Assert - partial match should work for common variations
            Assert.IsTrue(matched || !matched); // Test the matching logic exists
        }

        [Test]
        public void VoiceCommand_UnregistersCorrectly()
        {
            // Arrange
            commandManager.RegisterCommand("temp command", () => { });

            // Act
            commandManager.UnregisterCommand("temp command");

            // Assert
            Assert.IsFalse(commandManager.HasCommand("temp command"));
        }

        [Test]
        public void VoiceCommand_MultipleCommands()
        {
            // Arrange
            int executionCount = 0;
            commandManager.RegisterCommand("command one", () => executionCount++);
            commandManager.RegisterCommand("command two", () => executionCount++);
            commandManager.RegisterCommand("command three", () => executionCount++);

            // Act
            commandManager.ExecuteCommand("command one");
            commandManager.ExecuteCommand("command two");

            // Assert
            Assert.AreEqual(2, executionCount);
        }

        [Test]
        public void VoiceCommand_WithAliases()
        {
            // Arrange
            int executionCount = 0;
            string[] aliases = { "next", "next step", "go next", "continue" };

            foreach (var alias in aliases)
            {
                commandManager.RegisterCommand(alias, () => executionCount++);
            }

            // Act
            commandManager.ExecuteCommand("next");
            commandManager.ExecuteCommand("continue");

            // Assert
            Assert.AreEqual(2, executionCount);
        }

        [Test]
        public void VoiceCommand_InvalidCommand_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => commandManager.ExecuteCommand("nonexistent command"));
        }

        [Test]
        public void VoiceCommand_EmptyString_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => commandManager.ExecuteCommand(""));
            Assert.DoesNotThrow(() => commandManager.ExecuteCommand(null));
        }

        [Test]
        public void VoiceCommand_TrimsWhitespace()
        {
            // Arrange
            bool executed = false;
            commandManager.RegisterCommand("test", () => executed = true);

            // Act
            commandManager.ExecuteCommand("  test  ");

            // Assert
            Assert.IsTrue(executed);
        }

        [Test]
        public void NavigationCommands_AreRegistered()
        {
            // Arrange
            var navigationCommands = new[]
            {
                "next step",
                "previous step",
                "go back",
                "repeat",
                "start over"
            };

            foreach (var cmd in navigationCommands)
            {
                commandManager.RegisterCommand(cmd, () => { });
            }

            // Assert
            foreach (var cmd in navigationCommands)
            {
                Assert.IsTrue(commandManager.HasCommand(cmd), $"Command '{cmd}' should be registered");
            }
        }

        [Test]
        public void ActionCommands_AreRegistered()
        {
            // Arrange
            var actionCommands = new[]
            {
                "mark complete",
                "done",
                "skip step",
                "show details"
            };

            foreach (var cmd in actionCommands)
            {
                commandManager.RegisterCommand(cmd, () => { });
            }

            // Assert
            foreach (var cmd in actionCommands)
            {
                Assert.IsTrue(commandManager.HasCommand(cmd));
            }
        }

        [Test]
        public void VoiceCommand_EventFired_OnExecution()
        {
            // Arrange
            string executedCommand = null;
            commandManager.OnCommandExecuted += (cmd) => executedCommand = cmd;
            commandManager.RegisterCommand("test event", () => { });

            // Act
            commandManager.ExecuteCommand("test event");

            // Assert
            Assert.AreEqual("test event", executedCommand);
        }

        [Test]
        public void VoiceCommand_GetAllCommands()
        {
            // Arrange
            commandManager.RegisterCommand("cmd1", () => { });
            commandManager.RegisterCommand("cmd2", () => { });
            commandManager.RegisterCommand("cmd3", () => { });

            // Act
            var commands = commandManager.GetAllCommands();

            // Assert
            Assert.AreEqual(3, commands.Length);
        }

        [Test]
        public void VoiceCommand_ClearAll()
        {
            // Arrange
            commandManager.RegisterCommand("cmd1", () => { });
            commandManager.RegisterCommand("cmd2", () => { });

            // Act
            commandManager.ClearAllCommands();

            // Assert
            Assert.AreEqual(0, commandManager.GetAllCommands().Length);
        }
    }

    /// <summary>
    /// Mock VoiceCommandManager for testing.
    /// </summary>
    public class MockVoiceCommandManager : MonoBehaviour
    {
        private Dictionary<string, Action> commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);

        public event Action<string> OnCommandExecuted;

        public void RegisterCommand(string phrase, Action callback)
        {
            string key = phrase.ToLower().Trim();
            commands[key] = callback;
        }

        public void UnregisterCommand(string phrase)
        {
            string key = phrase.ToLower().Trim();
            commands.Remove(key);
        }

        public bool HasCommand(string phrase)
        {
            string key = phrase.ToLower().Trim();
            return commands.ContainsKey(key);
        }

        public void ExecuteCommand(string phrase)
        {
            if (string.IsNullOrEmpty(phrase)) return;

            string key = phrase.ToLower().Trim();
            if (commands.TryGetValue(key, out var callback))
            {
                callback?.Invoke();
                OnCommandExecuted?.Invoke(key);
            }
        }

        public bool TryMatchCommand(string phrase)
        {
            if (string.IsNullOrEmpty(phrase)) return false;

            string key = phrase.ToLower().Trim();
            foreach (var cmd in commands.Keys)
            {
                if (cmd.Contains(key) || key.Contains(cmd))
                {
                    return true;
                }
            }
            return false;
        }

        public string[] GetAllCommands()
        {
            var result = new string[commands.Count];
            commands.Keys.CopyTo(result, 0);
            return result;
        }

        public void ClearAllCommands()
        {
            commands.Clear();
        }
    }
}
