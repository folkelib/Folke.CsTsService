﻿using System;
using System.Collections.Generic;

namespace Folke.CsTsService.Nodes
{
    public class AssemblyNode
    {
        public List<ActionsGroupNode> Controllers { get; } = new List<ActionsGroupNode>();
        public Dictionary<string, ClassNode> Classes { get; set; } = new Dictionary<string, ClassNode>();
    }
}
