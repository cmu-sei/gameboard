// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;

namespace Gameboard.Api
{
    public class Challenge
    {
        public string Id { get; set; }
        public string SpecId { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public DateTimeOffset LastScoreTime { get; set; }
        public DateTimeOffset LastSyncTime { get; set; }
        public bool HasGamespaceDeployed { get; set; }
        public int Points { get; set; }
        public int Score { get; set; }
        public long Duration { get; set; }
        public ChallengeResult Result { get; set; }
        public TopoMojo.Api.Client.GameState State { get; set; }
    }

    public class NewChallenge
    {
        public string SpecId { get; set; }
        public string PlayerId { get; set; }
        public int Variant { get; set; }
    }

    public class ChangedChallenge
    {
        public string Id { get; set; }
    }

    public class TeamChallenge
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public int Points { get; set; }
        public int Score { get; set; }
        public long Duration { get; set; }
        public ChallengeResult Result { get; set; }
    }

    public class ConsoleRequest
    {
      public string Name { get; set; }
      public string SessionId { get; set; }
      public ConsoleAction Action { get; set; }
      public string Id => $"{Name}#{SessionId}";
    }

    public class ConsoleSummary
    {
      public string Id { get; set; }
      public string Name { get; set; }
      public string SessionId { get; set; }
      public string Url { get; set; }
      public bool IsRunning { get; set; }
    }

    public enum ConsoleAction
    {
      None,
      Ticket,
      Reset
    }
}
