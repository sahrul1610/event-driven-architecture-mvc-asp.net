﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PostService.Entities
{
    public class User
    {
        public int ID { get; set; }
        public required string Name { get; set; }
        public int Version { get; set; }
    }
}