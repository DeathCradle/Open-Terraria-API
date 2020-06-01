﻿/*
Copyright (C) 2020 DeathCradle

This file is part of Open Terraria API v3 (OTAPI)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <http://www.gnu.org/licenses/>.
*/
using MonoMod;
using OTAPI;
using System;
using System.Collections.Generic;

namespace Terraria
{
    class patch_RemoteClient
    {
        public Dictionary<string, object> Data { get; set; }

        public string ClientUUID
        {
            get => (Data[nameof(ClientUUID)] as string);
            set => Data[nameof(ClientUUID)] = value;
        }

        /** Begin Patch: Extra data */
        public extern void orig_ctor_RemoteClient();
        [MonoMod.MonoModConstructor]
        patch_RemoteClient()
        {
            orig_ctor_RemoteClient();
            Data = new Dictionary<string, object>();
        }

        public extern void orig_Reset();
        public void Reset()
        {
            orig_Reset();
            Data.Clear();
        }
        /** End Hook: Extra data */
    }
}