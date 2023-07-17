
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
////                                                                                             ////
////  rdnodude is firmware download application for Riscduino Series Chip                        ////
////                                                                                             ////
////  The riscduino Silicon project Details available at                                         ////
////  https://github.com/dineshannayya/riscduino.git                                             ////
////                                                                                             ////
////  Author(s):                                                                                 ////
////      - Dinesh Annayya, dinesh.annayya@gmail.com                                             ////
////                                                                                             ////
////  Revision :                                                                                 ////
////    0.1 - 17-July 2023, Dinesh A                                                             ////
////          Initial integration with firmware                                                  ////
/////////////////////////////////////////////////////////////////////////////////////////////////////


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;


namespace rdnodude
{
    class Program
    {
        struct s_result
        {
            public bool flag;
            public uint value;
        };

        static SerialPort _serialPort;
        static byte[] buffer = new byte[256];

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
                if (string.Equals(strlist[0], "Response:"))
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
                    iChar = _serialPort.ReadByte(); // Wait for Additinal New line >>
                    iChar = _serialPort.ReadByte(); // Wait for Additinal New line >>
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

            for (retry = 0; retry < 3; retry++)
            {  // For invalid response retry for 3 times
                _serialPort.DiscardInBuffer();
                string cmd = String.Format("rm {0:x8}\n", addr);
                //Console.WriteLine(cmd);
                _serialPort.Write(cmd);
                result = uartm_read_response(addr);
                if(result.flag == true) return result;
            }
            // After three retry exit
            _serialPort.Close();
            Environment.Exit(0);
            return result;

        }


        //####################################################
        //# Send wm <addr> <data> command and check the response
        //#  Return: Response Good  => '1' 
        //#  Return: Response Bad  => '0' 
        //####################################################
        static s_result uartm_wm_cmd(uint addr, uint data)
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
                result= uartm_write_response(addr,data);
                if (result.flag == true) return result;
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


            //uartm_wm_cmd(0x30080000,0x00000000)
            //uartm_wm_cmd(0x30080000,0x00000001)
            //uartm_wm_cmd(0x30080004,0x00001000)
            //uartm_wm_cmd(0x30020004,0x0000001F)
            uartm_wm_cmd(0x3000001c, 0x00000001);
            uartm_wm_cmd(0x30000020, 0x040c009f);
            result = uartm_rm_cmd(0x3000002c);
            Console.WriteLine("SPI Flash Device ID:0x{0:x8} ", result.value);
            if (result.value != 0x001640ef)
            {
                Console.WriteLine("ERROR: Invalid SPI Flash Device ID: {0} detected:", result.value);
                Environment.Exit(0);
            }
            else
            {
                Console.WriteLine("STATUS: Valid SPI Flash Device ID: {0} detected:", result.value);

            }
        }

        //#############################################
        //#  Sector Erase Command            
        //#############################################
        static void user_flash_chip_erase(){
            s_result result = new s_result();
            result.flag = false;

            Console.WriteLine("Flash Chip Erase: In Progress");
            uartm_wm_cmd(0x30080004,0x00001000);
            uartm_wm_cmd(0x3000001c,0x00000001);
            uartm_wm_cmd(0x30000020,0x00000006);
            uartm_wm_cmd(0x30000028,0x00000000);
            uartm_wm_cmd(0x3000001c,0x00000001);
            uartm_wm_cmd(0x30000020,0x002200d8);
            uartm_wm_cmd(0x30000024,0x00000000);
            uartm_wm_cmd(0x30000028,0x00000000);
            uartm_wm_cmd(0x3000001c,0x00000001);
            uartm_wm_cmd(0x30000020,0x040c0005);
            result.value = 0xFF;
            while (result.value != 0x00)
            {
                result = uartm_rm_cmd(0x3000002c);
            }
            Console.WriteLine("Flash Chip Erasing: Done");
        }


        //###########################
        //### Write 4 Byte
        //###########################
        static void user_flash_write_cmd()
        {
            uartm_wm_cmd(0x30080004, 0x00001000);
            uartm_wm_cmd(0x3000001c, 0x00000001);
        }

        //Flash Write Byte , addr : Address, exp_data: Expected Data, bcnt: valid byte cnt
        static void user_flash_write_data(uint addr, uint data, uint bcnt)
        {
            s_result result = new s_result();
            result.flag = false;

            //Console.WriteLine("Flash Write Addr: {0:x8} Data:{1:x8}", addr, data);
            uartm_wm_cmd(0x30000020, 0x00000006);
            uartm_wm_cmd(0x30000028, 0x00000000);
            //uartm_wm_cmd(0x3000001c,0x00000001);
            uartm_wm_cmd(0x30000020, 0x00270002 | bcnt << 24);
            uartm_wm_cmd(0x30000024, addr);
            uartm_wm_cmd(0x30000028, data);
            //uartm_wm_cmd(0x3000001c,0x00000001);
            uartm_wm_cmd(0x30000020, 0x040c0005);
            result.value = 0xFF;
            while (result.value != 0x00)
            {
                result = uartm_rm_cmd(0x3000002c);
            }
        }


        //#############################################
        //#  Page Read through Direct Access  (0X0B)          
        //#############################################
        static void user_flash_read_cmd()
        {
            uartm_wm_cmd(0x30080004, 0x00001000);
            uartm_wm_cmd(0x30000004, 0x4080000b);
            uartm_wm_cmd(0x30080004, 0x00000000);
        }

        //# Flash Read Byte , addr : Address, exp_data: Expected Data, bcnt: valid byte cnt
        static void user_flash_read_compare(uint addr, uint exp_data, uint bcnt)
        {
            uint mask = 0x00;
            s_result result = new s_result();
            result.flag = false;

            result = uartm_rm_cmd(addr);
            if (bcnt == 1) mask = 0x000000FF;
            else if (bcnt == 2) mask = 0x0000FFFF;
            else if (bcnt == 3) mask = 0x00FFFFFF;
            else if (bcnt == 4) mask = 0xFFFFFFFF;

            if ((exp_data & mask) == (result.value & mask))
            {
                Console.WriteLine("Flash Read Addr: {0:x8} Data:{1:x8} => Matched", addr, exp_data & mask);
            }
            else
            {
                Console.WriteLine("Flash Read Addr: {0:x8} Exp Data:{1:x8}  Rxd Data:{2:x8} => FAILED", addr, exp_data & mask, result.value & mask);
            }
        }

        //#############################################
        //#  User Reboot Command
        //#############################################
            static void user_reboot() {
                    Console.WriteLine("Reseting up User Risc Core");
                    uartm_wm_cmd(0x30080000,0x80000000); // Set Bit[31] = 1 to indicate user flashing to caravel
                    uartm_wm_cmd(0x30080000,0x80000001);
                    uartm_wm_cmd(0x30080004,0x00001000);
                    uartm_wm_cmd(0x30020004,0x0000001F);
             }

            //########################################
            //# User Risc Wake up
            //########################################
            static void user_risc_wakeup() {
                Console.WriteLine("Waking up User Risc Core");
                uartm_wm_cmd(0x30080000,0x80000000);
                uartm_wm_cmd(0x30080000,0x80000001);
                uartm_wm_cmd(0x30080004,0x00001000);
                uartm_wm_cmd(0x30020004,0x0000011F);
            }

//##############################
//# Flash Write
//##############################
            static void user_flash_progam(String file_path) {

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
                                                    user_flash_write_data(addr,dataout,4);
                                                    addr = addr+4;
                                                    ncnt = 0;
                                                    dataout = 0x00;
                                                }
                                            }
                                        }
                                        if(ncnt > 0 && ncnt < 4) {   // if line has less than 4 bytes
                                            Console.WriteLine("Writing Flash Partial DW, Address: {0:x8} Data: {1:x8} Cnt:{2:x1}", addr, dataout, ncnt);
                                            user_flash_write_data(addr,dataout,ncnt);
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
                                                    user_flash_read_compare(addr, dataout, 4);
                                                    addr = addr + 4;
                                                    ncnt = 0;
                                                    dataout = 0x00;
                                                }
                                            }
                                        }
                                        if (ncnt > 0 && ncnt < 4)
                                        {   // if line has less than 4 bytes
                                            user_flash_read_compare(addr, dataout, ncnt);
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


        static void Main(string[] args)
        {
            s_result result = new s_result();
            result.flag = false;

            if (args.Length == 3)
            {

                Console.WriteLine("COM PORT  = {0}", args[0]);
                Console.WriteLine("Baud Rate = {0}", args[1]);
                Console.WriteLine("Hex File  = {0}", args[2]);
            }
            else
            {
                Console.WriteLine("Fomat: rdnodude <COM> <BaudRate> <Hex File>");
                Environment.Exit(0);
            }

            String ComPort = args[0];
            int iBaudRate = Convert.ToInt32(args[1]);
            String HexFile = args[2];

            // Create a new SerialPort object with default settings.
            _serialPort = new SerialPort(ComPort, iBaudRate);
            _serialPort.Open();
            _serialPort.WriteTimeout = 300;
            _serialPort.ReadTimeout = 300;

            _serialPort.DiscardInBuffer();

            user_reboot(); 
            result = uartm_rm_cmd(0x30020000); //  # User Chip ID
            Console.WriteLine("Riscduino Chip ID:0x{0:x8} ", result.value);
            user_flash_device_id();
            user_flash_chip_erase();
            user_flash_progam(HexFile);
            user_flash_verify(HexFile);




            _serialPort.Close();
            

        }
    }
}
