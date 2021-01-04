﻿using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Regard.Backend.Common.Model
{
    public class SubscriptionPreference : IPreference
    {
        public string Key { get; set; }

        public string Value { get; set; }

        [Required]
        public int SubscriptionId { get; set; }

        public Subscription Subscription { get; set; }
    }
}
