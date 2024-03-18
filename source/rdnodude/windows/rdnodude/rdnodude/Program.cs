
/////////////////////////////////////////////////////////////////////////////////////////////////////
//// SPDX-FileCopyrightText: 2021 , Dinesh Annayya                                               ////
////                                                                                             ////
//// Licensed under the Apache License, Version 2.0 (the "License");                             ////
//// you may not use this file except in compliance with the License.                            ////
//// You may obtain a copy of the License at                                                     ////
////                                                                                             ////
////      http://www.apache.org/licenses/LICENSE-2.0                                             ////
////                                                                                             ////
//// Unless required by applicable law or agreed to in writing, software                         ////
//// distributed under the License is distributed on an "AS IS" BASIS,                           ////
//// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.                    ////
//// See the License for the specific language governing permissions and                         ////
//// limitations under the License.                                                              ////
//// SPDX-License-Identifier: Apache-2.0                                                         ////
//// SPDX-FileContributor: Created by Dinesh Annayya <dinesh.annayya@gmail.com>                  ////
////                                                                                             ////
/////////////////////////////////////////////////////////////////////////////////////////////////////
                                                                                                 
      rdnodude is firmware download application for Riscduino Series Chip                        
                                                                                                 
      The riscduino Silicon project Details available at                                         
      https://github.com/dineshannayya/riscduino.git                                             
                                                                                                 
      Author(s):                                                                                 
          - Dinesh Annayya, dinesh.annayya@gmail.com       

      Uart Command
         v1 :  wm 30020000 12345678
               cmd success>>

               rm 30020000
               Response: 12345678
                                                                                                 
         v3 :  wm 30020000 12345678
               cmd success

               rm 30020000
               12345678
                                      
                                                                                                 
      Revision :                                                                                 
        0.1 - 17-July 2023, Dinesh A                                                             
              Initial integration with firmware                                                  
        0.2 - 29-July 2023, Dinesh A                                                             
              A. DTR Toggle support added to reset the Riscduino chip                            
              B. As Flash Page Write is completes less than MiliSecond,                          
                 We are bypassing read back status check to reduce the flash download time.      
        0.3 - 17-Aug 2023, Dinesh A                                                              
             A. Quad SPI Read func added                                                         
        0.4 - 27 Aug 2023, Dinesh A                                                              
              Read compare Error count indication added                                          
        0.5 - 4 Sept 2023, Dinesh A                                                              
              A. Bank Switch Supported for Address Crossing 0xFFFF                               
              B. Flash Sector Erase function changed Chip Erase                                  
        0.6 - 6 Sept 2023, Dinesh A                                                              
              Auto Wakeup feature enabled                                                        
        0.7 - 19 Dec 2023, Dinesh A                                                              
         A. Enabled additional chip-id check to handle CI2306Q                                   
        0.71 - 19 Dec 2023, Dinesh A                                                             
         A.  changed the uart and caravel flash handshake bit at address 0x30080000 is changed   
               from bit[31] to [15] - Note: in 2306 chip bit[31] is tied to zero                 
        0.72 - 19 Dec 2023, Dinesh A                                                             
             icache/dcache enable/disable display added                                          
        0.8 - 19 Dec 2023, Dinesh A                                                              
             updated boot-up sequence with riscv-control reg value                               
        0.9 - 13 Feb 2024, Dinesh A                                                              
             Hard Reset is changed based on DTR toggle                                           
        1.0 - 18th Mar 2024, Dinesh A                                                            
              Revision added a option <v1/v2/v3>, v3 is needed for auto baud func                
************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Timers;


namespace rdnodude
{
    class Program
    {
        struct s_result
        {
            public bool flag;
            public uint value;
        };

        public const int SPI_FAST_READ         = 0x4080000b;
        public const int SPI_READ_DUAL_OUTPUT  = 0x40A4003b;
        public const int SPI_READ_DUAL_IO      = 0x509400bb;
        public const int SPI_READ_QUAD_IO      = 0x619800EB;

        public const int SPI_PAGE_WRITE        = 0x00270002;
        public const int SPI_PAGE_QUAD_WRITE   = 0x00270032;

        static string sVersion; // Version <v1/v2/v3>
        static SerialPort _serialPort;
        static byte[] buffer = new byte[256];

        static uint bank_addr = 0x00;

        static void delay(int Time_delay)
        {
            int i = 0;
            //  ameTir = new System.Timers.Timer();
            Timer _delayTimer = new System.Timers.Timer();
            _delayTimer.Interval = Time_delay;
            _delayTimer.AutoReset = false; //so that it only calls the method once
            _delayTimer.Elapsed += (s, args) => i = 1;
            _delayTimer.Start();
            while (i == 0) { };
        }

        // Get Read Response
        static s_result uartm_read_response(uint addr)
        {
            s_result result = new s_result();
            result.flag = false;

            try
            {
                string inString = _serialPort.ReadLine();
                char[] spearator = { ' ', '\n' };
                String[] strlist = inString.Split(spearator, 4, StringSplitOptions.None);
                if (string.Equals(sVersion, "v3"))
                {
                    //Console.WriteLine("Read  Addr: {0:x8} Data: {1:x8}", addr, strlist[1]);
                    result.flag = true;
                    result.value = Convert.ToUInt32(strlist[0], 16);
                    return result;

                } else if (string.Equals(strlist[0], "Response:"))
                {
                    //Console.WriteLine("Read  Addr: {0:x8} Data: {1:x8}", addr, strlist[1]);
                    result.flag = true;
                    result.value = Convert.ToUInt32(strlist[1], 16);
                    return result;
                }
                else
                {
                    Console.WriteLine("Invalid Response: {} Received", inString);
                    return result;
                }
            }
            catch (TimeoutException)
            {
            }

            return result;
        }

        // Wait for Write Response
        static s_result uartm_write_response(uint addr, uint data)
        {
            s_result result = new s_result();
            result.flag = false;
            int iChar;

            try
            {
                string inString = _serialPort.ReadLine();
                char[] spearator = { '>' };
                String[] strlist = inString.Split(spearator, 4, StringSplitOptions.None);
                if (string.Equals(strlist[0], "cmd success"))
                {
                    //Console.WriteLine("Write Addr: {0:x8} Data:{1:x8}", addr, data);
                    result.flag = true;
                    if (string.Equals(sVersion, "v3") == false) // If Version is not V3
                    {
                        iChar = _serialPort.ReadByte(); // Wait for Additinal New line >>
                        iChar = _serialPort.ReadByte(); // Wait for Additinal New line >>
                    }
                    return result;
                }
                else
                {
                    Console.WriteLine("Invalid Response: {0}Received", inString);
                }
            }
            catch (TimeoutException)
            {
            }

            return result;
        }




        //####################################################
        //# Send rm <addr> command and check the response
        //#  Return: Response Good  => true & <Read Data>
        //#  Return: Response Bad  => false & '00'
        //####################################################
        static s_result uartm_rm_cmd(uint addr)
        {
            int retry;

            s_result result = new s_result();
            result.flag = false;
            result.value = 0;
            try
            {

                for (retry = 0; retry < 3; retry++)
                {  // For invalid response retry for 3 times
                    _serialPort.DiscardInBuffer();
                    string cmd = String.Format("rm {0:x8}\n", addr);
                    //Console.WriteLine(cmd);
                    _serialPort.Write(cmd);
                    result = uartm_read_response(addr);
                    if (result.flag == true) return result;
                }
                // After three retry exit
                _serialPort.Close();
                Environment.Exit(0);
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
                Environment.Exit(0);
                return result;
            }



        }


        //####################################################
        //# Send wm <addr> <data> command and check the response
        //#  Return: Response Good  => '1' 
        //#  Return: Response Bad  => '0' 
        //####################################################
        static s_result uartm_wm_cmd(uint addr, uint data,bool bRdCheck)
        {
            int retry;

            s_result result = new s_result();
            result.flag = false;
            result.value = 0;


            for (retry = 0; retry < 3; retry++)
            {
                _serialPort.DiscardInBuffer();
                string cmd = String.Format("wm {0:x8} {1:x8}\n", addr, data);
                _serialPort.Write(cmd);
                if (bRdCheck)
                {
                    result = uartm_write_response(addr, data);
                    if (result.flag == true) return result;
                }
                else
                {
                     System.Threading.Thread.Sleep(1);

                    result.flag = true;
                    return result;
                }
                //string inString = Encoding.Default.GetString(buffer);
                //Console.WriteLine(byte_array);
            }
            // After three retry exit
            _serialPort.Close();
            Environment.Exit(0);
            return result;
        }


        // Reading Device ID(0x9F)
        static void user_flash_device_id()
        {
            s_result result = new s_result();
            result.flag = false;


            //uartm_wm_cmd(0x30080000,0x00000000,true);
            //uartm_wm_cmd(0x30080000,0x00000001,true);
            //uartm_wm_cmd(0x30080004,0x00001000,true);
            //uartm_wm_cmd(0x30020004,0x0000001F,true);
            uartm_wm_cmd(0x3000001c, 0x00000001,true);
            uartm_wm_cmd(0x30000020, 0x040c009f,true);
            result = uartm_rm_cmd(0x3000002c);
            Console.WriteLine("SPI Flash Device ID:0x{0:x8} ", result.value);
            if (result.value != 0x001640ef)
            {
                Console.WriteLine("ERROR: Invalid SPI Flash Device ID: 0x{0} detected:", result.value);
                Environment.Exit(0);
            }
            else
            {
                Console.WriteLine("STATUS: Valid SPI Flash Device ID: 0x{0} detected:", result.value);

            }
        }

        //#############################################
        //#  Sector Erase Command            
        //#############################################
        static void user_flash_chip_erase(){
            s_result result = new s_result();
            result.flag = false;

            Console.WriteLine("Flash Chip Erase: In Progress");
            bank_addr = 0x1000;
            uartm_wm_cmd(0x30080004,bank_addr, true);
            uartm_wm_cmd(0x3000001c,0x00000001,true);
            uartm_wm_cmd(0x30000020,0x00000006,true);
            uartm_wm_cmd(0x30000028,0x00000000,true);
            uartm_wm_cmd(0x3000001c,0x00000001,true);
            uartm_wm_cmd(0x30000020,0x000000c7, true);
            uartm_wm_cmd(0x30000028,0x00000000,true);
            uartm_wm_cmd(0x3000001c,0x00000001,true);
            uartm_wm_cmd(0x30000020,0x040c0005,true);
            result.value = 0xFF;
            while (result.value != 0x00)
            {
                result = uartm_rm_cmd(0x3000002c);
            }
            Console.WriteLine("Flash Chip Erasing: Done");
        }


        //###########################
        //### Write CMD
        //###########################
        static void user_flash_write_cmd()
        {
            bank_addr = 0x1000;
            uartm_wm_cmd(0x30080004, bank_addr, true);
            uartm_wm_cmd(0x3000001c, 0x00000001,true);
        }

        //Flash Write Byte , addr : Address, exp_data: Expected Data, bcnt: valid byte cnt
        static void user_flash_write_data(uint addr, uint data, uint bcnt, bool bCheckEnb)
        {
            s_result result = new s_result();
            result.flag = false;

            //Console.WriteLine("Flash Write Addr: {0:x8} Data:{1:x8}", addr, data);
            // WREN = 0x06 Command (Write Enable)
            uartm_wm_cmd(0x30000020, 0x00000006, bCheckEnb);
            uartm_wm_cmd(0x30000028, 0x00000000, bCheckEnb);
            //uartm_wm_cmd(0x3000001c,0x00000001,true);
            // PAGE-WR =0x02
            uartm_wm_cmd(0x30000020, 0x00270002 | bcnt << 24, bCheckEnb);
            uartm_wm_cmd(0x30000024, addr, bCheckEnb);
            uartm_wm_cmd(0x30000028, data, bCheckEnb);
            /** We have Masked Read Back for Flash write, as our process is slow, if needed we need to enable this one
            uartm_wm_cmd(0x30000020, 0x040c0005, true);
            result.value = 0xFF;
            while (result.value != 0x00)
            {
                result = uartm_rm_cmd(0x3000002c);
            }
             ****/
        }



        //#############################################
        //#  Page Read through Direct Access  (0X0B)          
        //#############################################
        static void user_flash_read_cmd()
        {
            bank_addr = 0x0000;
            uartm_wm_cmd(0x30080004, bank_addr, true);
        }

        //# Flash Read Byte , addr : Address, exp_data: Expected Data, bcnt: valid byte cnt
        static s_result user_flash_read_compare(uint addr, uint exp_data, uint bcnt)
        {
            uint mask = 0x00;
            s_result result = new s_result();
            result.flag = false;

             // Check if there is change in bank address
            uint new_bank = addr >> 16;
            if(bank_addr != new_bank) { 
                 bank_addr = addr >> 16;
                 Console.WriteLine("Changing Bank Address: {0:x8}", bank_addr);
                 uartm_wm_cmd(0x30080004,bank_addr,true);
            }
            result = uartm_rm_cmd(addr);
            if (bcnt == 1) mask = 0x000000FF;
            else if (bcnt == 2) mask = 0x0000FFFF;
            else if (bcnt == 3) mask = 0x00FFFFFF;
            else if (bcnt == 4) mask = 0xFFFFFFFF;

            if ((exp_data & mask) == (result.value & mask))
            {
                Console.WriteLine("Flash Read Addr: {0:x8} Data:{1:x8} => Matched", addr, exp_data & mask);
                result.flag = false;
            }
            else
            {
                Console.WriteLine("Flash Read Addr: {0:x8} Exp Data:{1:x8}  Rxd Data:{2:x8} => FAILED", addr, exp_data & mask, result.value & mask);
                result.flag = true;
            }

            return result;
        }

        //#############################################
        //#  User Reboot Command
        //#############################################
            static void user_reboot() {
                    s_result risc_ctrl = new s_result();

                    bank_addr = 0x00001000;
                    uartm_wm_cmd(0x30080004, bank_addr, true);
                    // copy the risc control config, as this will get configured by caravel based on chip
                    risc_ctrl = uartm_rm_cmd(0x30020008);

                    Console.WriteLine("Reseting up User Risc Core");
                    // From CI2306 Onwards Set Bit[15] = 1 to indicate user flashing in progress to  caravel Riscv core.
                    // In CI2206Q, Bit[31] used  to indicate user flashing in progress to  caravel Riscv core.
                    uartm_wm_cmd(0x30080000,0x80008000,true); 
                    uartm_wm_cmd(0x30080000,0x80008001,true);

                    // copy back the risc control
                    uartm_wm_cmd(0x30020008, risc_ctrl.value, true); 
                    uartm_wm_cmd(0x30020004,0x0000001F,true);
                    // Setting Serial Flash to Quad Mode
                    uartm_wm_cmd(0x30000004, 0x619800EB, true);
                    // Setting Serial SRAM to Quad Mode
                    uartm_wm_cmd(0x3000000C, 0x408a0003, true);
                    uartm_wm_cmd(0x30000010, 0x708a0002, true);
             }

            //########################################
            //# User Risc Wake up
            //########################################
            static void user_risc_wakeup() {
                s_result risc_ctrl = new s_result();

                Console.WriteLine("Waking up User Risc Core");
                bank_addr = 0x00001000;
                uartm_wm_cmd(0x30080004, bank_addr, true);
                // copy the risc control config, as this will get configured by caravel based on chip
                risc_ctrl = uartm_rm_cmd(0x30020008);

                uartm_wm_cmd(0x30080000, 0x00000000, true); 
                uartm_wm_cmd(0x30080000, 0x00000001, true);
                uartm_wm_cmd(0x30020008, risc_ctrl.value, true); // copy back the risc control
                uartm_wm_cmd(0x30020004, 0x0000001F, true);
                // Setting Serial Flash to Quad Mode
                uartm_wm_cmd(0x30000004, 0x619800EB, true);
                // Setting Serial SRAM to Quad Mode
                uartm_wm_cmd(0x3000000C, 0x408a0003, true);
                uartm_wm_cmd(0x30000010, 0x708a0002, true);
                // Remove Riscv Core Reset
                uartm_wm_cmd(0x30020004, 0x0000011F, false);

            }


        //---------------------------------
// Reading Riscduino Chip ID
//---------------------------------
static void user_chip_id()  {
    s_result result = new s_result();

    // Bit [11:8] mapping: Chip ID
    //    0 -  YIFIVE (MPW-2)
    //    1 -  Riscdunio (MPW-3)
    //    2 -  Riscdunio (MPW-4)
    //    3 -  Riscdunio (MPW-5)
    //    4 -  Riscdunio (MPW-6)
    //    5 -  Riscdunio (MPW-7)
    //    6 -  Riscdunio (MPW-8)
    //    7 -  Riscdunio (MPW-9)
    //  
    result = uartm_rm_cmd(0x30020000);
    if (result.value == 0x82681501) {
        Console.WriteLine("Riscduino-SCore MPW-7 Equivalent Chip => 0x{0:x8} [GOOD]", result.value);
        result.flag = true;
    }else if (result.value == 0x81681601) {
        Console.WriteLine("Riscduino-SCore MPW-8 Equivalent Chip => 0x{0:x8} [GOOD]", result.value);
        result.flag = true;
    }else if (result.value == 0x81681701) {
        Console.WriteLine("Riscduino-SCore MPW-9 Equivalent Chip => 0x{0:x8} [GOOD]", result.value);
        result.flag = true;
    }else if (result.value == 0x82682501) {
        Console.WriteLine("Riscduino-DCore MPW-7 Equivalent Chip => 0x{0:x8} [GOOD]", result.value);
        result.flag = true;
    }else if (result.value == 0x82682601) {
        Console.WriteLine("Riscduino-DCore MPW-8 Equivalent Chip => 0x{0:x8} [GOOD]", result.value);
        result.flag = true;
    }else if (result.value == 0x82682701) {
        Console.WriteLine("Riscduino-DCore MPW-9 Equivalent Chip => 0x{0:x8} [GOOD]", result.value);
        result.flag = true;
    } else if (result.value == 0x82684501) {
        Console.WriteLine("Riscduino-QCore MPW-7 Equivalent Chip => 0x{0:x8} [GOOD]", result.value);
        result.flag = true;
    }else if (result.value == 0x81684601) {
        Console.WriteLine("Riscduino-QCore MPW-8 Equivalent Chip => 0x{0:x8} [GOOD]", result.value);
        result.flag = true;
    }else if (result.value == 0x81684701) {
        Console.WriteLine("Riscduino-QCore MPW-9 Equivalent Chip => 0x{0:x8} [GOOD]", result.value);
        result.flag = true;
    } else {
        Console.WriteLine("Riscduino Chip ID => 0x{0:x8} [GOOD]", result.value);
        Environment.Exit(0);
    }
}

//#############################################
//#  Display the riscdduino signature
//#############################################
static void riscduino_signature()
{
    s_result result = new s_result();
    uartm_wm_cmd(0x30080004, 0x00001000, true);
    result = uartm_rm_cmd(0x30020000);
    Console.WriteLine("Riscduino Chip ID      :: 0x{0:x8}", result.value);
    result = uartm_rm_cmd(0x30020040);
    Console.WriteLine("Riscduino Signature    :: 0x{0:x8}", result.value);
    result = uartm_rm_cmd(0x30020044);
    Console.WriteLine("Riscduino Release Date :: 0x{0:x8}", result.value);
    result = uartm_rm_cmd(0x30020048);
    Console.WriteLine("Riscduino Version      :: 0x{0:x8}", result.value);
    
    result = uartm_rm_cmd(0x30020008);
    Console.WriteLine("Riscduino Cache Ctrl   :: 0x{0:x8} ", result.value);
    if ((result.value & 0x04000000) != 0) Console.WriteLine("Riscduino icache       ::  Disabled");
    else Console.WriteLine("Riscduino icache       ::  Enabled");
    if ((result.value & 0x08000000) != 0) Console.WriteLine("Riscduino dcache       ::  Disabled");
    else Console.WriteLine("Riscduino dcache       ::  Enabled");

}

//##############################
//# Flash Write
//##############################
            static void user_flash_progam(String file_path,bool bCheckEnb) {

                uint addr,dataout,ncnt;
                int nbytes, total_bytes;
                String substring;
                nbytes = 0;
                total_bytes = 0;
                addr = 0;

                Console.WriteLine("User Flash Write Phase Started");
                user_flash_write_cmd();
        
                String line;
                try
                {

                    if (System.IO.File.Exists(file_path)) {
                        // Read file using StreamReader. Reads file line by line
                        using(System.IO.StreamReader file = new System.IO.StreamReader(file_path)) {
                            while (((line = file.ReadLine()) != null))
                            {
                                if (line != null)
                                {
                                    //write the line to console window
                                    //Console.WriteLine(line);
                                    if (line[0] == '@')
                                    { // Check for Address
                                        substring = line.Substring(1, line.Length-1);
                                        addr =  Convert.ToUInt32(substring, 16);
                                        Console.WriteLine("setting address to {0:x8}",addr);
                                        total_bytes += nbytes;
                                        nbytes = 0;
                                    } else { // If Data
                                        //Console.WriteLine(x);
                                        // Assumed Max 32 Data Per line
                                        char[] spearator = { ' ' };
                                        String[] strlist = line.Split(spearator, 32, StringSplitOptions.None);
                                        dataout = 0x00;
                                        ncnt = 0;

                                        foreach (String data in strlist) {
                                            if (String.Equals(data,"") == false) {
                                                uint tData = Convert.ToUInt32(data, 16);
                                                int tShift = (8 * Convert.ToInt32(ncnt));
                                                dataout |= tData << tShift; 
                                                ncnt = ncnt + 1;
                                                nbytes = nbytes+1;
                                                if(ncnt == 4){
                                                    Console.WriteLine("Writing Flash Address: {0:x8} Data: {1:x8}", addr, dataout);
                                                    user_flash_write_data(addr, dataout, 4, bCheckEnb);
                                                    addr = addr+4;
                                                    ncnt = 0;
                                                    dataout = 0x00;
                                                }
                                            }
                                        }
                                        if(ncnt > 0 && ncnt < 4) {   // if line has less than 4 bytes
                                            Console.WriteLine("Writing Flash Partial DW, Address: {0:x8} Data: {1:x8} Cnt:{2:x1}", addr, dataout, ncnt);
                                            user_flash_write_data(addr, dataout, ncnt, bCheckEnb);
                                        }
                                    }

                                    if (line[0] != '@' && line[0] != ' ' && nbytes >= 256)
                                    {
                                        total_bytes += nbytes;
                                        Console.WriteLine("addr {0:x8}: flash page write successful",addr);
                                        if(nbytes > 256) {
                                            Console.WriteLine("ERROR: *** Data over 256 hit");
                                        } else {
                                            nbytes =0;
                                        }

                                    } 
                                }
                             }
                            // Managing the Last Less than 256 Byte Access
                            if (nbytes > 0) {
                                total_bytes += nbytes;
                            }

                            file.Close();
                        }   
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine("Exception: " + e.Message);
                }
                finally
                {
                    //Console.WriteLine("Executing finally block.");
                }
  
            Console.WriteLine("total_bytes = {0}",total_bytes);

        }

            //##############################
            //# Flash Read and Verify 
            //##############################
            static void user_flash_verify(String file_path)
            {

                uint addr, dataout, ncnt;
                int nbytes, total_bytes;
                String substring;
                nbytes = 0;
                total_bytes = 0;
                addr = 0;
                s_result result = new s_result();
                uint iErrCnt = 0;

                Console.WriteLine("User Flash Read back and verify Started");
                user_flash_read_cmd();

                String line;
                try
                {

                    if (System.IO.File.Exists(file_path))
                    {
                        // Read file using StreamReader. Reads file line by line
                        using (System.IO.StreamReader file = new System.IO.StreamReader(file_path))
                        {
                            while (((line = file.ReadLine()) != null))
                            {
                                if (line != null)
                                {
                                    //write the line to console window
                                    //Console.WriteLine(line);
                                    if (line[0] == '@')
                                    { // Check for Address
                                        substring = line.Substring(1, line.Length - 1);
                                        addr = Convert.ToUInt32(substring, 16);
                                        Console.WriteLine("setting address to {0:x8}", addr);
                                        total_bytes += nbytes;
                                        nbytes = 0;
                                        dataout = 0;
                                    }
                                    else
                                    { // If Data
                                        //Console.WriteLine(x);
                                        // Assumed Max 32 Data Per line
                                        char[] spearator = { ' ' };
                                        String[] strlist = line.Split(spearator, 32, StringSplitOptions.None);
                                        dataout = 0x00;
                                        ncnt = 0;

                                        foreach (String data in strlist)
                                        {
                                            if (String.Equals(data, "") == false)
                                            {
                                                uint tData = Convert.ToUInt32(data, 16);
                                                int tShift = (8 * Convert.ToInt32(ncnt));
                                                dataout |= tData << tShift;
                                                ncnt = ncnt + 1;
                                                nbytes = nbytes + 1;
                                                if (ncnt == 4)
                                                {
                                                    result = user_flash_read_compare(addr, dataout, 4);
                                                    if(result.flag == true)  iErrCnt ++;
                                                    addr = addr + 4;
                                                    ncnt = 0;
                                                    dataout = 0x00;
                                                }
                                            }
                                        }
                                        if (ncnt > 0 && ncnt < 4)
                                        {   // if line has less than 4 bytes
                                            result = user_flash_read_compare(addr, dataout, ncnt);
                                            if (result.flag == true) iErrCnt++;
                                        }
                                    }

                                    if (line[0] != '@' && line[0] != ' ' && nbytes >= 256)
                                    {
                                        total_bytes += nbytes;
                                        Console.WriteLine("addr {0:x8}: flash page Read verify Completed", addr);
                                        if (nbytes > 256)
                                        {
                                            Console.WriteLine("ERROR: *** Data over 256 hit");
                                        }
                                        else
                                        {
                                            nbytes = 0;
                                        }

                                    }
                                }
                            }
                            // Managing the Last Less than 256 Byte Access
                            if (nbytes > 0)
                            {
                                total_bytes += nbytes;
                            }
                            if (iErrCnt > 0)
                            {
                                Console.WriteLine("ERROR: Total Read compare failure {0} detected \n", iErrCnt);
                            }

                            file.Close();
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: " + e.Message);
                }
                finally
                {
                    //Console.WriteLine("Executing finally block.");
                }

                Console.WriteLine("total_bytes = {0}", total_bytes);

            }

        // Generate Hardware reset with toggle DTR from Low to High
        static void hard_reset() {

            _serialPort.DtrEnable = true;
            System.Threading.Thread.Sleep(100);
            _serialPort.DtrEnable = false;


        }

        // In V3- Since Chip is Auto Baud mode, We need to send '\n' 
        // to device to detect the baud rate
        static void auto_bauddetect()
        {

            if (string.Equals(sVersion, "v3"))
            {
                _serialPort.Write("\n");
            }
        }


        static void Main(string[] args)
        {
            s_result result = new s_result();
            result.flag = false;

            Console.WriteLine("runodude (Rev:1.0)- A Riscduino firmware downloading application");

            if (args.Length == 4)
            {

                Console.WriteLine("Version   = {0}", args[0]);
                Console.WriteLine("COM PORT  = {0}", args[1]);
                Console.WriteLine("Baud Rate = {0}", args[2]);
                Console.WriteLine("Hex File  = {0}", args[3]);
            }
            else
            {
                Console.WriteLine("Fomat: rdnodude <Version: v1/v2/v3> <COM> <BaudRate> <Hex File>");
                Environment.Exit(0);
            }

            sVersion = args[0];
            String ComPort = args[1];
            int iBaudRate = Convert.ToInt32(args[2]);
            String HexFile = args[3];

            // Create a new SerialPort object with default settings.
            _serialPort = new SerialPort(ComPort, iBaudRate);
            _serialPort.Open();
            _serialPort.WriteTimeout = 300;
            _serialPort.ReadTimeout = 300;


            hard_reset();
            System.Threading.Thread.Sleep(2000);

            auto_bauddetect(); 
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();

            user_reboot();
            result = uartm_rm_cmd(0x30020000); //  # User Chip ID
            Console.WriteLine("Riscduino Chip ID:0x{0:x8} ", result.value);
            user_chip_id();
            user_flash_device_id();
            user_flash_chip_erase();
            user_flash_progam(HexFile,true);
            user_flash_verify(HexFile);
            riscduino_signature();

            hard_reset();
            //user_risc_wakeup();




            _serialPort.Close();
            

        }
    }
}
