﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace MpegTS
{
    /// <summary>
    /// Represents a data stream (like video) spanning multiple Ts packets.
    /// 
    /// https://en.wikipedia.org/wiki/Packetized_elementary_stream
    /// </summary>
    public class PacketizedElementaryStream
    {
        /// <summary>
        /// look this value after the AdaptationField in the stream to 
        /// indicate the beginning of a video stream payload.
        /// </summary>
        public const uint VideoStartCode =  0xE0010000;//reversed byte order for little-endian systems

        //public static bool IsPES(TsPacket ts)
        //{
        //    bool val = false;

        //    val = (BitConverter.ToInt32(ts.data, ts.PayloadStart) == VideoStartCode);

        //    return val;
        //}

        private Queue<TsPacket> packets;

        private int ExtensionLen
        {
            get
            {
                return
                    BitConverter.ToUInt16(Header, packets.Peek().PayloadStart + 4);
            }
        }

        private byte[] Header
        {
            get { return packets.Peek().data; }
        }
        private int payloadIndex;


        public bool IsComplete { get; private set; }
        /// <summary>
        /// Continuity counter is used to track if TS packets are missing.
        /// For TS packet 0...n, TSn+1.CC == (TSn.CC + 1)
        /// </summary>
        private int lastCC;

        public bool IsValid
        {
            get
            {
                int start = packets.Peek().PayloadStart;
                if (start < 4 || start+4 > Header.Length)
                    return false;
                else
                    return BitConverter.ToUInt32(Header, packets.Peek().PayloadStart) == VideoStartCode;
            }
        }

        public PacketizedElementaryStream(TsPacket first)
        {
            IsComplete = true;//we assume it is complete until we prove it is not

            packets = new Queue<TsPacket>(4);
            packets.Enqueue(first);

            lastCC = first.ContinuityCounter;

            payloadIndex = first.PayloadStart;
        }

        /// <summary>
        /// Add another Ts packet to this stream to be re-built later.
        /// </summary>
        /// <param name="next"></param>
        public void Add(TsPacket next)
        {
            packets.Enqueue(next);

            //check for missed packets
            if (lastCC < 15 &&
                next.ContinuityCounter != lastCC + 1)
            {
                IsComplete = false;
            }
            else if (lastCC == 15 && next.ContinuityCounter != 0)
                IsComplete = false;

            lastCC = next.ContinuityCounter;
        }

        /// <summary>
        /// byte count of this PES in the first TsPacket
        /// </summary>
        public int PesLen
        {
            get
            {
                var p = packets.Peek();

                return p.data.Length - payloadIndex;
            }
        }

        public int PesHeaderLen
        {
            get { return Header[payloadIndex + 8]; }
        }

        /// <summary>
        /// Presentation time stamp
        /// </summary>
        public long PTS
        {
            get
            {
                if (!HasPts || PesHeaderLen < 5)
                    return 0;

                int ptsi = payloadIndex+9;
                var data = Header;//hang onto a ref to the data buffer.
                //ByteBuffer hd = ByteBuffer.wrap(headerData);
                long pts = (((data[ptsi++] & 0x0e) << 29)
                            | ((data[ptsi++] & 0xff) << 22)
                            | ((data[ptsi++] & 0xfe) << 14)
                            | ((data[ptsi++] & 0xff) << 7)
                            | ((data[ptsi++] & 0xfe) >> 1));

                return pts;
            }
        }

        public bool HasPts
        {
            get { return (Header[payloadIndex + 7] & 0x80) > 0; }
        }

        public int PayloadStart
        {
            get { return StartCodeLen + PesExtLen + PesHeaderLen; }
        }

        public const int PesExtLen = 5;//bytes of ext PES header 
        public const int StartCodeLen = 4;

        public byte[] GetPayload()
        {
            //need to pull out NAL's now to pass to the decoder
            //http://stackoverflow.com/questions/1685494/what-does-this-h264-nal-header-mean
            //H.264 spec docs:  see Table 7-1 for NALu ID's
            //http://www.itu.int/rec/T-REC-H.264-201304-S

            //for now, let's just try to strip out all the header bytes, leaving only video stream bytes

            if (!IsValid)//we don't have a PES start code!
                return new byte[0];

            int startOfPayload = PayloadStart;//get this now, so we don't try to access the queue later.

            //start with this packet's payload len...
            int firstLen = PesLen - startOfPayload;//-startcode/prefix(4) -header(5byte, usually)
            //int vidLen = firstLen;
            TsPacket p;

            //let's do proper clean up... might be our leak
            using (var ms = new System.IO.MemoryStream(firstLen * packets.Count))//try to get an estimate of the size needed to avoid re-sizing
            {

                //create a tmp que to stuff the packets back into so we don't lose them
                Queue<TsPacket> tmpQ = new Queue<TsPacket>(packets.Count);

                bool start = true;
                //get total byte count for reassembled PES
                while (packets.Count > 0)
                {
                    tmpQ.Enqueue(p = packets.Dequeue());//hang onto the ref

                    //vidLen += p.data.Length - p.PayloadStart;

                    //get a memoryStream to the payload
                    using (var s = p.GetPayload())
                    {
                        if (!start)
                        {
                            //if (packets.Count > 0)
                            s.CopyTo(ms);//no PES header stuff in following packets
                                         //else//need to trim trailing 0's
                                         //{
                                         //    using (var s2 = p.GetPayload(false))
                                         //        s2.CopyTo(ms);
                                         //}
                        }
                        else//first packet
                        {
                            s.Position = startOfPayload;//move past the header/start bytes

                            s.CopyTo(ms);
                        }
                    }

                    start = false;
                }

                //swap queue's
                var tq = packets;
                packets = tmpQ;
                tmpQ = tq;

                return ms.ToArray();
            }//end using block, dispose the stream
        }
    }
}