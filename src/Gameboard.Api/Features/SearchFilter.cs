// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
using System;

namespace Gameboard.Api;

public class SearchFilter
{
    public string Term { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public string Sort { get; set; }    // Could possibly be deleted
    public SortDirection? SortDirection { get; set; }
    public string[] Filter { get; set; } = [];
    // The column to order the result ticket list on
    public string OrderItem { get; set; }
    // Whether the list is in descending or ascending order
    public bool IsDescending { get; set; }
    public bool HasTerm => string.IsNullOrWhiteSpace(Term).Equals(false);
}

public class Entity
{
    public string Id { get; set; }
}
