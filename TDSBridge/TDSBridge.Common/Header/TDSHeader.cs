using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TDSBridge.Common.Header
{
    public class TDSHeader
    {
        public const int HEADER_SIZE = 8;

        private byte[] _buffer = new byte[HEADER_SIZE];

         public TDSHeader(byte[] bPacket)
        {
            Array.Copy(bPacket, 0, this._buffer, 0, HEADER_SIZE);
        }

        public HeaderType Type { get { return (HeaderType)_buffer[0]; } }
        public byte StatusBitMask { get { return _buffer[1]; } }

        public int LengthIncludingHeader { 
            get 
            {
                return ((int)_buffer[2]) * 0x100 + ((int)_buffer[3]);       
            } 
        }

        public int PayloadSize
        {
            get
            {
                return LengthIncludingHeader - HEADER_SIZE;
            }
        }

        public byte this[int idx]
        {
            get { return _buffer[idx]; }
            set { _buffer[idx] = value; }
        }

        public override string ToString()
        {
            return GetType().FullName +
                "[Type=" + Type +
                ";StatusBitMask=" + StatusBitMask +
                ";LengthIncludingHeader=" + LengthIncludingHeader +
                ";PayloadSize=" + PayloadSize +
                "]";
        }
    }
}
