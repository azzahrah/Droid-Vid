﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MpegTS
{
    /// <summary>
    /// Common TS packet id's... NOT exhaustive!
    /// </summary>
    public enum PID:byte
    {
        PAT = 0x00,
        PMT = 0x10,
        H264Video = 0x11,

        /// <summary>
        /// this PID is actually called "navigation data" but in our case, this means KLV 
        /// </summary>
        KLV = 0x21,

        /// <summary>
        /// AVi specific
        /// </summary>
        StillImage = 0x91
    }


    /// <summary>
    /// TS scrambling options
    /// </summary>
    public enum Scrambling : byte
    {
        None =0x00,
        Reserved = 0x40,
        EvenKey = 0x80,
        OddKey = 0xC0
    }

    /// <summary>
    /// Represents a 188 byte chunk of media suitable for streaming (a Transport Stream packet)
    /// https://en.wikipedia.org/wiki/MPEG_transport_stream
    /// 
    /// </summary>
    public class TsPacket
    {
        internal protected byte[] data;

        /// <summary>
        /// the start/sync byte for MpegTS packets.
        /// </summary>
        public const byte SyncByte = 0x47;

        public bool IsValid { get { return data[09] == SyncByte; } }

        /// <summary>
        /// indicates error in transport stream
        /// </summary>
        public bool HasError
        {
            get { return (data[1] & 0x80) > 0; }
        }

        /// <summary>
        /// Indicates this packet is the start of a 
        /// payload message and forth-coming TsPackets 
        /// *could* be merged with this one (until another TsPacket says it is the start).<para/>
        /// In the case that a single TsPacket contains all payload data,
        /// this *will* be true.  Must look @ next packet to know.
        /// </summary>
        public bool IsPayloadUnitStart
        {
            get { return (data[1] & 0x40) > 0; }
        }


        public bool TransportPriority
        {
            get { return (data[1] & 0x20) > 0; }
        }

        /// <summary>
        /// Identifies the type of payload contained in this packet <see cref="MpegTS.PID"/>
        /// </summary>
        public PID PID
        {
            //**technically, this is > 1 byte... but for now, this works, and is faster.
            get { return (PID)data[2]; }
        }

        public byte RawPID { get { return data[2]; } }

        public Scrambling ScramblingCtl
        {
            get { return (Scrambling)(data[3] & 0xC0); }
        }

        /// <summary>
        /// tells us if Adaptation field is present
        /// http://etherguidesystems.com/Help/SDOs/MPEG/Semantics/MPEG-2/adaptation_field.aspx
        /// </summary>
        public bool AdaptationFieldCtlPresent
        {
            get
            {
                int val = (data[3] & 0x30)>>4;//mask off the two bits
                return (val == 0x3 || val ==0x2);
            }
        }

        public int ContinuityCounter
        {
            get { return data[3] & 0x0F; }
        }

        public AdaptationField Adaptation
        { get { return new AdaptationField(this); } }


        public TsPacket(byte[] rawData)
        {
            data = rawData;
        }
        
        /// <summary>
        /// allows re-use of this object w/o call to new.
        /// </summary>
        /// <param name="rawData"></param>
        public void Reinit(byte[] rawData)
        {
            data = rawData; 
        }


        /// <summary>
        /// read only payload for this Ts packet
        /// </summary>   
        public System.IO.MemoryStream GetPayload(bool trimTrailingZeros = false)
        {
            int start = PayloadStart;

            int end = data.Length - start;
            if (trimTrailingZeros)
            {
                int last = data.Length - 1;
                int val = data[last];

                while (val == 0)
                    val = data[--last];

                end -= (data.Length - last - 1);//don't count trailing zeros in the length
            }

            return new System.IO.MemoryStream(data, start, end, false);
        }

        /// <summary>
        /// returns the index of the buffer where the paload starts
        /// </summary>
        internal int PayloadStart
        {
            get
            {
                if (AdaptationFieldCtlPresent)
                    return 4//TsPacket header len
                        + 1 //the byte storing Adaptation Field length
                        + data[4];//the value of AdaptionField byte count
                else
                    return 4;
            }
        }
    }
}