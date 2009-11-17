﻿/* 
 * Copyright (C) 1999-2009 John Källén.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; see the file COPYING.  If not, write to
 * the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.
 */

using Decompiler.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Decompiler.Environments.MacOS
{
// http://developer.apple.com/legacy/mac/library/documentation/mac/MoreToolbox/MoreToolbox-99.html

    public class ResourceFork
    {
        private byte[]  image;
        private ResourceTypeCollection rsrcTypes;

        uint rsrcDataOff;
        uint rsrcMapOff;
        uint dataSize;
        uint mapSize;


        public ResourceFork(byte [] bytes)
        {
            image = bytes;

             rsrcDataOff = ProgramImage.ReadBeUint32(bytes, 0);
             rsrcMapOff = ProgramImage.ReadBeUint32(bytes, 4);
             dataSize = ProgramImage.ReadBeUint32(bytes, 8);
             mapSize = ProgramImage.ReadBeUint32(bytes, 0x0C);

            rsrcTypes =  new ResourceTypeCollection(image, rsrcMapOff, mapSize);
            
        }

        public ResourceTypeCollection ResourceTypes
        {
            get { return rsrcTypes; }
        }

        public class ResourceType
        {
            private string name;
            private ReferenceCollection references;

            public ResourceType(byte[] bytes, string name, uint offset, uint nameListOffset, int crsrc)
            {
                references = new ReferenceCollection(bytes, offset, nameListOffset, crsrc);
                this.name = name;
            }

            public string Name
            {
                get { return name; }
            }

            public ReferenceCollection References
            {
                get { return references; }
            }
        }

        public class ResourceReference
        {
            private ushort rsrcID; 
            private string name; 
            private uint offset;
            
            public ResourceReference(ushort rsrcID, string name, uint offset)
            {
                this.rsrcID = rsrcID;
                this.name = name;
                this.offset = offset;
            }

            public ushort ResourceID { get { return rsrcID; } }
            public string Name { get { return name; } }
            public uint DataOffset { get { return offset; } }
        }

        public class ResourceTypeCollection : ICollection<ResourceType>
        {
            private byte[] bytes;
                uint rsrcTypeListOff ;
                uint rsrcNameListOff;
                int crsrcTypes;

            public ResourceTypeCollection(byte [] bytes, uint offset, uint size)
            {
                this.bytes = bytes;
                rsrcTypeListOff = offset + ProgramImage.ReadBeUint16(bytes, (int)offset + 0x18) + 2u;
                rsrcNameListOff = offset + ProgramImage.ReadBeUint16(bytes, (int) offset + 0x1A);
                crsrcTypes = ProgramImage.ReadBeUint16(bytes, (int) offset + 0x1C) + 1;
            }

            #region ICollection<ResourceType> Members

            void ICollection<ResourceType>.Add(ResourceType item)
            {
                throw new NotSupportedException();
            }

            void ICollection<ResourceType>.Clear()
            {
                throw new NotSupportedException();
            }

            bool ICollection<ResourceType>.Contains(ResourceType item)
            {
                throw new NotSupportedException();
            }

            public void CopyTo(ResourceType[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public int Count
            {
                get { return crsrcTypes; }
            }

            public bool IsReadOnly
            {
                get { return true; }
            }

            bool ICollection<ResourceType>.Remove(ResourceType item)
            {
                throw new NotSupportedException();
            }

            #endregion

            #region IEnumerable<ResourceType> Members

            public IEnumerator<ResourceType> GetEnumerator()
            {
                int offset = (int) rsrcTypeListOff;
                for (int i = 0; i < crsrcTypes; ++i)
                {
                    string rsrcTypeName = Encoding.ASCII.GetString(bytes, offset, 4);
                    int crsrc = ProgramImage.ReadBeUint16(bytes, offset + 4) + 1;
                    uint rsrcReferenceListOffset = rsrcTypeListOff + ProgramImage.ReadBeUint16(bytes, offset + 6);
                    yield return new ResourceType(bytes, rsrcTypeName, rsrcReferenceListOffset, rsrcNameListOff, crsrc);
                    offset += 8;
                }
            }

            #endregion

            #region IEnumerable Members

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            #endregion
        }

        public class ReferenceCollection : ICollection<ResourceReference>
        {
            private byte [] bytes;
            private uint offset;
            private uint nameListOffset;
            private int count;

            public ReferenceCollection(byte [] bytes, uint offset, uint nameListOffset, int count)
            {
                this.bytes = bytes;
                this.offset  = offset;
                this.nameListOffset = nameListOffset;
                this.count = count;
            }

            #region ICollection<ResourceReference> Members

            void ICollection<ResourceReference>.Add(ResourceReference item)
            {
                throw new NotSupportedException();
            }

            void ICollection<ResourceReference>.Clear()
            {
                throw new NotSupportedException();
            }

            bool ICollection<ResourceReference>.Contains(ResourceReference item)
            {
                throw new NotImplementedException();
            }

            void ICollection<ResourceReference>.CopyTo(ResourceReference[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public int Count
            {
                get { return count; }
            }

            public bool IsReadOnly
            {
                get { return true; }
            }

            bool ICollection<ResourceReference>.Remove(ResourceReference item)
            {
                throw new NotImplementedException();
            }

            #endregion

            #region IEnumerable<ResourceReference> Members

            public IEnumerator<ResourceReference> GetEnumerator()
            {
                int offset = (int) this.offset;
                for (int i = 0; i < count; ++i)
                {
                    ushort rsrcID = ProgramImage.ReadBeUint16(bytes, offset);
                    uint nameOff = nameListOffset + ProgramImage.ReadBeUint16(bytes, offset + 2);
                    uint dataOff = ProgramImage.ReadBeUint32(bytes, offset + 4);
                    yield return new ResourceReference(rsrcID, "", dataOff);

                    offset += 0x0C;
                }
            }

            #endregion

            #region IEnumerable Members

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }

            #endregion
        }

        public void Dump()
        {
            foreach (ResourceType type in ResourceTypes)
            {
                Debug.WriteLine(string.Format("Resource type {0}", type.Name));
                foreach (ResourceReference rsrc in type.References)
                {
                    Debug.WriteLine(string.Format("   Resource ID: {0:X4}", rsrc.ResourceID));
                    Debug.WriteLine(string.Format("   Name: {0}", rsrc.Name));
                    Debug.WriteLine(string.Format("   Offset: {0:X8}", rsrc.DataOffset));
                    Debug.WriteLine("    ====");
                }
            }
        }
    }
}