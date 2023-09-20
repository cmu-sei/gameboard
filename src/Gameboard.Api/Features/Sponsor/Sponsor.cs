// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Gameboard.Api
{
    public class Sponsor
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Logo { get; set; }
    }

    public class NewSponsor
    {
        public IFormFile LogoFile { get; set; }
        public string Name { get; set; }
    }

    public class ChangedSponsor
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public IFormFile LogoFile { get; set; }
    }
}
