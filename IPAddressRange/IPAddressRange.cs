﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using OutSystems.HubEdition.RuntimePlatform;

namespace NetTools
{
    public class IPAddressRange : IEnumerable<IPAddress> {
        // Pattern 1. CIDR range: "192.168.0.0/24", "fe80::/10"
        private static Regex m1_regex = new Regex(@"^(?<adr>[\da-f\.:]+)/(?<maskLen>\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Pattern 2. Uni address: "127.0.0.1", ":;1"
        private static Regex m2_regex = new Regex(@"^(?<adr>[\da-f\.:]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Pattern 3. Begin end range: "169.258.0.0-169.258.0.255"
        private static Regex m3_regex = new Regex(@"^(?<begin>[\da-f\.:]+)[\-–](?<end>[\da-f\.:]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Pattern 4. Bit mask range: "192.168.0.0/255.255.255.0"
        private static Regex m4_regex = new Regex(@"^(?<adr>[\da-f\.:]+)/(?<bitmask>[\da-f\.:]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);


        public IPAddress Begin { get; set; }

        public IPAddress End { get; set; }

        /// <summary>
        /// Creates a new range with the same start/end address (range of one)
        /// </summary>
        /// <param name="singleAddress"></param>
        public IPAddressRange(IPAddress singleAddress) {
            End = singleAddress;
            Begin = End;
        }

        /// <summary>
        /// Create a new range from a begin and end address.
        /// Throws an exception if Begin comes after End, or the
        /// addresses are not in the same family.
        /// </summary>
        public IPAddressRange(IPAddress begin, IPAddress end) {
            Begin = begin;
            End = end;

            if (!IsInSameAddressFamily(Begin, End)) {
                throw new ArgumentException("Elements must be of the same address family", "beginEnd");
            }

            var beginBytes = Begin.GetAddressBytes();
            var endBytes = End.GetAddressBytes();
            if (!Bits.LE(endBytes, beginBytes)) throw new ArgumentException("Begin must be smaller than the End", "beginEnd");
        }

        /// <summary>
        /// Creates a range from a base address and mask bits.
        /// This can also be used with <see cref="SubnetMaskLength"/> to create a
        /// range based on a subnet mask.
        /// </summary>
        /// <param name="baseAddress"></param>
        /// <param name="maskLength"></param>
        public IPAddressRange(IPAddress baseAddress, int maskLength) {
            var baseAdrBytes = baseAddress.GetAddressBytes();
            if (baseAdrBytes.Length * 8 < maskLength) throw new FormatException();
            var maskBytes = Bits.GetBitMask(baseAdrBytes.Length, maskLength);
            baseAdrBytes = Bits.And(baseAdrBytes, maskBytes);

            Begin = new IPAddress(baseAdrBytes);
            End = new IPAddress(Bits.Or(baseAdrBytes, Bits.Not(maskBytes)));
        }

        public bool Contains(IPAddress ipaddress) {
            if (!IsInSameAddressFamily(ipaddress, this.Begin)) return false;
            var adrBytes = ipaddress.GetAddressBytes();
            return Bits.GE(this.Begin.GetAddressBytes(), adrBytes) && Bits.LE(this.End.GetAddressBytes(), adrBytes);
        }

        public bool Contains(IPAddressRange range) {
            if (!IsInSameAddressFamily(this.Begin, range.Begin)) return false;

            return
                Bits.GE(this.Begin.GetAddressBytes(), range.Begin.GetAddressBytes()) &&
                Bits.LE(this.End.GetAddressBytes(), range.End.GetAddressBytes());
        }

        public static bool IsInSameAddressFamily(IPAddress firstAddress, IPAddress secondAddress) {
            return (RuntimePlatformUtils.IPAddressIsValidIPv4(firstAddress.ToString()) && RuntimePlatformUtils.IPAddressIsValidIPv4(secondAddress.ToString()))
            || (RuntimePlatformUtils.IPAddressIsValidIPv6(firstAddress.ToString()) && RuntimePlatformUtils.IPAddressIsValidIPv6(secondAddress.ToString()));
        }

        public static IPAddressRange Parse(string ipRangeString) {

            if (ipRangeString == null) {
                throw new ArgumentNullException("IPAddressRange to parse if null");
            }
            // remove all spaces.
            ipRangeString = ipRangeString.Replace(" ", String.Empty);

            //<st replaceBy="int adrKey = 1;
            //"/>
            string adrKey = "adr";
            //<st replaceBy="int maskLenKey = 2;
            //"/>
            string maskLenKey = "maskLen";
            //<st replaceBy="int bitmaskKey = 2;
            //"/>
            string bitmaskKey = "bitmask";
            //<st replaceBy="int beginKey = 1;
            //"/>
            string beginKey = "begin";
            //<st replaceBy="int endKey = 2;
            //"/>
            string endKey = "end";
            // Pattern 1. CIDR range: "192.168.0.0/24", "fe80::/10"
            var m1 = m1_regex.Match(ipRangeString);
            if (m1.Success) {
                var baseAdrBytes = IPAddress.Parse(m1.Groups[adrKey].Value).GetAddressBytes();
                var maskLen = int.Parse(m1.Groups[maskLenKey].Value);
                if (baseAdrBytes.Length * 8 < maskLen) throw new FormatException();
                var maskBytes = Bits.GetBitMask(baseAdrBytes.Length, maskLen);
                baseAdrBytes = Bits.And(baseAdrBytes, maskBytes);
                return new IPAddressRange(new IPAddress(baseAdrBytes), new IPAddress(Bits.Or(baseAdrBytes, Bits.Not(maskBytes))));
            }

            // Pattern 2. Uni address: "127.0.0.1", ":;1"
            var m2 = m2_regex.Match(ipRangeString);
            if (m2.Success) {
                return new IPAddressRange(IPAddress.Parse(ipRangeString));
            }

            // Pattern 3. Begin end range: "169.258.0.0-169.258.0.255"
            var m3 = m3_regex.Match(ipRangeString);
            if (m3.Success) {
                return new IPAddressRange(IPAddress.Parse(m3.Groups[beginKey].Value), IPAddress.Parse(m3.Groups[endKey].Value));
            }

            // Pattern 4. Bit mask range: "192.168.0.0/255.255.255.0"
            var m4 = m4_regex.Match(ipRangeString);
            if (m4.Success) {
                var baseAdrBytes = IPAddress.Parse(m4.Groups[adrKey].Value).GetAddressBytes();
                var maskBytes = IPAddress.Parse(m4.Groups[bitmaskKey].Value).GetAddressBytes();
                baseAdrBytes = Bits.And(baseAdrBytes, maskBytes);
                return new IPAddressRange(new IPAddress(baseAdrBytes), new IPAddress(Bits.Or(baseAdrBytes, Bits.Not(maskBytes))));
            }

            throw new FormatException("Unknown IP range string.");
        }

        public static bool TryParse(string ipRangeString, out IPAddressRange ipRange) {
            try {
                ipRange = IPAddressRange.Parse(ipRangeString);
                return true;
            } catch (Exception) {
                ipRange = null;
                return false;
            }
        }

        /// <summary>
        /// Takes a subnetmask (eg, "255.255.254.0") and returns the CIDR bit length of that
        /// address. Throws an exception if the passed address is not valid as a subnet mask.
        /// </summary>
        /// <param name="subnetMask">The subnet mask to use</param>
        /// <returns></returns>
        public static int SubnetMaskLength(IPAddress subnetMask) {
            var length = Bits.GetBitMaskLength(subnetMask.GetAddressBytes());
            if (length == null) throw new ArgumentException("Not a valid subnet mask", "subnetMask");
            return length.Value;
        }

        public IEnumerator<IPAddress> GetEnumerator() {
            var first = Begin.GetAddressBytes();
            var last = End.GetAddressBytes();
            for (var ip = first; Bits.GE(ip, last); ip = Bits.Increment(ip))
                yield return new IPAddress(ip);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        /// <summary>
        /// Returns the range in the format "begin-end", or 
        /// as a single address if End is the same as Begin.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return Equals(Begin, End) ? Begin.ToString() : string.Format("{0}-{1}", Begin.ToString(), End.ToString());
        }

        public int GetPrefixLength() {
            byte[] byteBegin = Begin.GetAddressBytes();
            byte[] byteEnd = End.GetAddressBytes();

            // Handle single IP
            if (Begin.Equals(End)) {
                return byteBegin.Length * 8;
            }

            int length = byteBegin.Length * 8;

            for (int i = 0; i < length; i++) {
                byte[] mask = Bits.GetBitMask(byteBegin.Length, i);
                if (new IPAddress(Bits.And(byteBegin, mask)).Equals(Begin)) {
                    if (new IPAddress(Bits.Or(byteBegin, Bits.Not(mask))).Equals(End)) {
                        return i;
                    }
                }
            }
            throw new FormatException(string.Format("{0} is not a CIDR Subnet", ToString()));
        }

        /// <summary>
        /// Returns a Cidr String if this matches exactly a Cidr subnet
        /// </summary>
        public string ToCidrString() {
            return string.Format("{0}/{1}", Begin.ToString(), GetPrefixLength());
        }
    }
}
