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
////    0.2 - 26 July 2023, Dinesh A                                                             ////
////          As current Flash write phase is around 4 Minute, To reduce the time, we are        ////
////          skipping write back response function and added just delay function                ////
////    0.4 - 27 Aug 2023, Dinesh A                                                              ////
////          Read compare Error count indication added                                          ////
////    0.5 - 4 Sept 2023, Dinesh A                                                              ////
////          Memory Write/Read to to SRAM Location (0x08xx_xxxx) support added                  ////
/////////////////////////////////////////////////////////////////////////////////////////////////////

#include <fcntl.h>
#include <stdio.h>
#include <unistd.h>
#include <stdint.h>
#include <termios.h>
#include <stdlib.h>
#include <string.h>
#include <sys/ioctl.h>

struct s_result
{
   char flag;
   unsigned int value;
};

unsigned char buffer[256];

unsigned char wr_resp[14] = {'c','m','d',' ','s','u','c','c','e','s','s','\n','>','>'};


struct s_result uartm_wm_cmd    (int fd,unsigned int addr, unsigned int data,char bEnbRdCheck);
void            user_reboot     ();
int             open_serial_port(const char * device, uint32_t baud_rate);
int             write_port      (int fd, uint8_t * buffer, size_t size);
ssize_t         read_port       (int fd, uint8_t * buffer, size_t size);
void            flush_rx        (int fd);
void            flush_tx        (int fd);

unsigned int bank_addr = 0x00;
//------------------------------------------------
// Flush Serial Port Rx Buffer
//------------------------------------------------
void flush_rx(int fd) {
   ioctl(fd, TCIFLUSH); // flush receive
}

//------------------------------------------------
// Flush Serial Port Tx Buffer
//------------------------------------------------
void flush_tx(int fd) {
   ioctl(fd, TCOFLUSH); // flush transmit
}


/*******************************
  Convert Byte Array to String
**********************************/

char * ByteArrayToString(uint8_t * buffer, int iSize) {
    char *InString;
    for(int i = 0; i < iSize; i++) {
      InString += sprintf(InString, "%c", buffer[i]);
    
    }
    return InString;
}
/*******************************
  Extract Specific Integer Valiue of SubString
**********************************/

struct  s_result  ExtractReadResponse(char *buffer,int iSize) {
     struct s_result result;
     char substring[8];
     result.flag = 0;

        //printf("Response Received: %s \n", buffer);
        if(strncmp(buffer,"Response:",9) == 0) {
            strncpy(substring,buffer+10,8);
            sscanf(substring,"%x",&result.value);
            //printf("Received Data: %08x\n",result.value);
            result.flag = 1;
        } else {
             printf("Invalid Response: %s Received\n", buffer);
             exit(0);
            
        }
        return result;
}

//------------------------------
// Get Read Response
//------------------------------
 struct s_result uartm_read_response(int fd, uint addr)
 {
     struct s_result result;
     result.flag = 0;

     int iSize = read_port(fd,buffer, 19);
     result = ExtractReadResponse(buffer,iSize);

     return result;
 }


        //####################################################
        //# Send rm <addr> command and check the response
        //#  Return: Response Good  => true & <Read Data>
        //#  Return: Response Bad  => false & '00'
        //####################################################
        struct s_result uartm_rm_cmd(int fd,unsigned int addr)
        {
            int retry;

            struct s_result result;
            result.flag = 0;
            result.value = 0;

            for (retry = 0; retry < 3; retry++)
            {  // For invalid response retry for 3 times
                 flush_rx(fd);
                 sprintf(buffer,"rm %08x\n", addr);
                 //printf("Sending: %s",buffer);
                 write_port(fd, buffer, 12);
                result = uartm_read_response(fd, addr);
                if(result.flag == 1) return result;
            }
            // After three retry exit
            close(fd);
            exit(0);
            return result;

        }

//------------------------------------------------
// Wait for Write Response
//------------------------------------------------
struct s_result uartm_write_response(int fd,unsigned int  addr, unsigned int  data) {
            struct s_result result;
            result.flag = 0;
            int iChar;

            read_port(fd,buffer, 14);
            if(strncmp(buffer,"cmd success",11) != 0) {
                 printf("Invalid Response:%s Received\n",buffer);
                 result.flag = 0;
            } else {
                 //printf("Valid Response:%s Received\n",buffer);
                result.flag = 1;
            }

            return result;
        }


//####################################################
//# Send wm <addr> <data> command and check the response
//#  Return: Response Good  => '1' 
//#  Return: Response Bad  => '0' 
//####################################################
        struct s_result uartm_wm_cmd(int fd,unsigned int addr, unsigned int data, char bEnbRdCheck)
        {
            int retry;

            struct s_result result;
            result.flag = 0;
            result.value = 0;


            for (retry = 0; retry < 3; retry++)
            {
                flush_rx(fd);
                sprintf(buffer,"wm %08x %08x\n", addr, data);
                //printf("Sending Command: %s\n",buffer);
                write_port(fd, buffer, 21);
                if(bEnbRdCheck) {
                    result= uartm_write_response(fd,addr,data);
                    if (result.flag == 1) return result;
                } else {
                   usleep(15000);
                   flush_rx(fd);
                   result.flag = 1;
                   return result;
                }
            }
            // After three retry exit
            close(fd);
            exit(0);
            return result;
        }

//#############################################
//#  User Reboot Command
//#############################################
void user_reboot(int fd) {
    printf("Reseting up User Risc Core\n");
    uartm_wm_cmd(fd,0x30080000,0x80000000,1); // Set Bit[31] = 1 to indicate user flashing to caravel
    uartm_wm_cmd(fd,0x30080000,0x80000001,1);
    bank_addr = 0x00001000;
    uartm_wm_cmd(fd,0x30080004,bank_addr,1);
    uartm_wm_cmd(fd,0x30020004,0x0000001F,1);
     // Setting Serial Flash to Quad Mode
     uartm_wm_cmd(fd,0x30000004, 0x619800EB,1);
     // Setting Serial SRAM to Quad Mode
     uartm_wm_cmd(fd,0x3000000C, 0x408a0003,1);
     uartm_wm_cmd(fd,0x30000010, 0x708a0002,1);
}


 
// Opens the specified serial port, sets it up for binary communication,
// configures its read timeouts, and sets its baud rate.
// Returns a non-negative file descriptor on success, or -1 on failure.
int open_serial_port(const char * device, uint32_t baud_rate)
{
  int fd = open(device, O_RDWR | O_NOCTTY);
  if (fd == -1)
  {
    perror(device);
    return -1;
  }
 
  // Flush away any bytes previously read or written.
  int result = tcflush(fd, TCIOFLUSH);
  if (result)
  {
    perror("tcflush failed");  // just a warning, not a fatal error
  }
 
  // Get the current configuration of the serial port.
  struct termios options;
  result = tcgetattr(fd, &options);
  if (result)
  {
    perror("tcgetattr failed");
    close(fd);
    return -1;
  }
 
  // Turn off any options that might interfere with our ability to send and
  // receive raw binary bytes.
  options.c_iflag &= ~(INLCR | IGNCR | ICRNL | IXON | IXOFF);
  options.c_oflag &= ~(ONLCR | OCRNL);
  options.c_lflag &= ~(ECHO | ECHONL | ICANON | ISIG | IEXTEN);
 
  // Set up timeouts: Calls to read() will return as soon as there is
  // at least one byte available or when 100 ms has passed.
  options.c_cc[VTIME] = 1;
  options.c_cc[VMIN] = 0;
 
  // This code only supports certain standard baud rates. Supporting
  // non-standard baud rates should be possible but takes more work.
  switch (baud_rate)
  {
  case 4800:   cfsetospeed(&options, B4800);   break;
  case 9600:   cfsetospeed(&options, B9600);   break;
  case 19200:  cfsetospeed(&options, B19200);  break;
  case 38400:  cfsetospeed(&options, B38400);  break;
  case 57600:  cfsetospeed(&options, B57600); break;
  case 115200: cfsetospeed(&options, B115200); break;
  case 230400: cfsetospeed(&options, B230400); break;
  default:
    fprintf(stderr, "warning: baud rate %u is not supported, using 9600.\n",
      baud_rate);
    cfsetospeed(&options, B9600);
    break;
  }
  cfsetispeed(&options, cfgetospeed(&options));
 
  result = tcsetattr(fd, TCSANOW, &options);
  if (result)
  {
    perror("tcsetattr failed");
    close(fd);
    return -1;
  }
 
  return fd;
}
 
// Writes bytes to the serial port, returning 0 on success and -1 on failure.
int write_port(int fd, uint8_t * buffer, size_t size)
{
  ssize_t result = write(fd, buffer, size);
  if (result != (ssize_t)size)
  {
    perror("failed to write to port");
    return -1;
  }
  return 0;
}
 
// Reads bytes from the serial port.
// Returns after all the desired bytes have been read, or if there is a
// timeout or other error.
// Returns the number of bytes successfully read into the buffer, or -1 if
// there was an error reading.
ssize_t read_port(int fd, uint8_t * buffer, size_t size)
{
  size_t received = 0;
  while (received < size)
  {
    ssize_t r = read(fd, buffer + received, size - received);
    if (r < 0)
    {
      perror("failed to read from serial port\n");
      return -1;
    }
    if (r == 0)
    {
       printf("Exiting Serial port Loop due to Time Out\n");
      // Timeout
      break;
    }
    received += r;
  }
  return received;
}

//---------------------------------
// Reading Flash Device ID(0x9F)
//---------------------------------
void user_flash_device_id(int fd)
{
    struct s_result result;
    result.flag = 0;


    //uartm_wm_cmd(0x30080000,0x00000000,1)
    //uartm_wm_cmd(0x30080000,0x00000001,1)
    //uartm_wm_cmd(0x30080004,0x00001000,1)
    //uartm_wm_cmd(0x30020004,0x0000001F,1)
    uartm_wm_cmd(fd,0x3000001c, 0x00000001,1);
    uartm_wm_cmd(fd,0x30000020, 0x040c009f,1);
    result = uartm_rm_cmd(fd,0x3000002c);
    //printf("SPI Flash Device ID:0x%08x\n", result.value);
    if (result.value != 0x001640ef)
    {
        printf("SPI Flash Device ID => 0x%08x [BAD]", result.value);
        exit(0);
    }
    else
    {
        printf("SPI Flash Device ID => 0x%08x [GOOD]\n", result.value);
        result.flag = 1;
    }
}
 
//---------------------------------
// Reading Riscduino Chip ID
//---------------------------------
void user_chip_id(int fd)
{
    struct s_result result;
    result.flag = 0;

    result = uartm_rm_cmd(fd,0x30020000);
    if (result.value == 0x82682501)
    {
        printf("Riscduino Chip ID => 0x%08x [GOOD]\n", result.value);
        result.flag = 1;
    }
    else
    {
        printf("Riscduino Chip ID => %d [BAD]\n", result.value);
        exit(0);
    }
}

//#############################################
//#  Sector Erase Command            
//#############################################
void user_flash_chip_erase(int fd){
     struct s_result result;
     result.flag = 0;

     printf("Flash Chip Erase: In Progress\n");
     bank_addr = 0x00001000;
     uartm_wm_cmd(fd,0x30080004,bank_addr,1);
     uartm_wm_cmd(fd,0x3000001c,0x00000001,1);
     uartm_wm_cmd(fd,0x30000020,0x00000006,1);
     uartm_wm_cmd(fd,0x30000028,0x00000000,1);
     uartm_wm_cmd(fd,0x3000001c,0x00000001,1);
     uartm_wm_cmd(fd,0x30000020,0x000000c7,1);
     uartm_wm_cmd(fd,0x30000028,0x00000000,1);
     uartm_wm_cmd(fd,0x3000001c,0x00000001,1);
     uartm_wm_cmd(fd,0x30000020,0x040c0005,1);
     result.value = 0xFF;
     while (result.value != 0x00)
     {
         result = uartm_rm_cmd(fd,0x3000002c);
     }
     printf("Flash Chip Erasing: Done\n");
}

//###########################
//### Write 4 Byte
//###########################
static void user_flash_write_cmd(int fd)
{
    bank_addr = 0x00001000;
    uartm_wm_cmd(fd,0x30080004,bank_addr,1);
    uartm_wm_cmd(fd,0x3000001c, 0x00000001,1);
}


//-----------------------------------------
// Extract Number of words per line
//-----------------------------------------
int num_word_per_line(char * Instring) {
int i,count=1;
int ignoreSpace = 0;

for (i = 0; i < strlen(Instring)-2; i++)
    {
        if (Instring[i] == ' ' && (i != (strlen(Instring)-3)))
        {
            if (!ignoreSpace)
            {       
                count++;
                ignoreSpace = 1;
            }
        }
        else 
        {
            ignoreSpace = 0;
        }
    }
    //if (!ignoreSpace)
    //    count++;

    return count;

}

//------------------------------------------------------------------------------
//Flash Write Byte , addr : Address, exp_data: Expected Data, bcnt: valid byte cnt
//------------------------------------------------------------------------------
void user_flash_write_data(int fd,unsigned int addr, unsigned int data, unsigned int bcnt,char bEnbRdCheck) {
    struct s_result result ;
    result.flag = 0;

    //Console.WriteLine("Flash Write Addr: {0:x8} Data:{1:x8}", addr, data);
    uartm_wm_cmd(fd,0x30000020, 0x00000006,bEnbRdCheck);
    uartm_wm_cmd(fd,0x30000028, 0x00000000,bEnbRdCheck);
    //uartm_wm_cmd(fd,0x3000001c,0x00000001,bEnbRdCheck);
    uartm_wm_cmd(fd,0x30000020, 0x00270002 | bcnt << 24,bEnbRdCheck);
    uartm_wm_cmd(fd,0x30000024, addr,bEnbRdCheck);
    uartm_wm_cmd(fd,0x30000028, data,bEnbRdCheck);
    //uartm_wm_cmd(fd,0x3000001c,0x00000001,bEnbRdCheck);
    uartm_wm_cmd(fd,0x30000020, 0x040c0005,bEnbRdCheck);

    result.value = 0xFF;
    while (result.value != 0x00)
    {
        result = uartm_rm_cmd(fd,0x3000002c);
    }
}

void user_sram_write_data(int fd,unsigned int addr, unsigned int data,unsigned int bcnt,char bEnbRdCheck) {
    struct s_result result ;
    result.flag = 0;

    //Console.WriteLine("SRAM Write Addr: {0:x8} Data:{1:x8}", addr, data);
    bank_addr = addr>> 16;
    uartm_wm_cmd(fd,0x30080004,bank_addr,1);
    uartm_wm_cmd(fd,addr,data,bEnbRdCheck);

}



//##############################
//# Flash Write
//##############################
void user_flash_progam(int fd,const char *file_path) {
   unsigned int addr,dataout,ncnt;
   int nbytes, total_bytes;
   char Instring[256];
   char substring[8];
   unsigned int tData;
   nbytes = 0;
   total_bytes = 0;
   addr = 0;

   printf("User Flash Write Phase Started\n");
   user_flash_write_cmd(fd);
        
   FILE* f_open;
   f_open = fopen(file_path, "r");

    while (fgets(Instring,256, f_open)) {
        nbytes = num_word_per_line(Instring);
        //printf("Line:%d :%ld :word:%d : %s\n",nbytes,strlen(Instring), nbytes,Instring);
        if(Instring[0] == '@') {
            nbytes = 0; // Indicate this is address byte, not data byte
            strncpy(substring,Instring+1,8);
            sscanf(substring,"%x",&addr);
            printf("setting address to 0x%08x\n",addr);
        } else {
            ncnt = 0;
            dataout = 0;
            for(int i =0; i < nbytes; i++) {
               strncpy(substring,Instring+(i*3),2);
               substring[2] =0x00;
               sscanf(substring,"%x",&tData);
               int tShift = (8 * i);
               dataout |= tData << tShift; 
               //printf("SubString:%d:%d:%d:%s:%x:%x\n",nbytes,i,ncnt,substring,tData,dataout);
               ncnt = ncnt + 1;
               total_bytes ++;
               if(ncnt == 4){
                   if(addr < 0x08000000) {
                      printf("Writing Flash Address: 0x%08x Data: 0x%08x\n", addr, dataout);
                      
                      user_flash_write_data(fd,addr,dataout,4,1);
                   } else {
                      user_sram_write_data(fd,addr,dataout,4,1);
                   }
                   addr = addr+4;
                   ncnt = 0;
                   dataout = 0x00;
                }
            }
            if(ncnt > 0 && ncnt < 4) {   // if line has less than 4 bytes
                 printf("Writing Flash Partial DW, Address: 0x%08x Data: %08x Cnt:%d", addr, dataout, ncnt);
                 if(addr < 0x08000000) {
                    printf("Writing Flash Address: 0x%08x Data: 0x%08x\n", addr, dataout);
                    user_flash_write_data(fd,addr,dataout,ncnt,1);
                 } else {
                    user_sram_write_data(fd,addr,dataout,ncnt,1);
                 }
            }

            if (Instring[0] != '@' && Instring[0] != ' ' && nbytes >= 256) {
                 printf("addr 0x%08x: flash page write successful",addr);
                 if(nbytes > 256) {
                    printf("ERROR: *** Data over 256 hit");
                    exit(0);
                 } else {
                     nbytes =0;
                 }
            } 


        }
    }
    printf("total_bytes = %d\n",total_bytes);
    fclose(f_open);

}

//#############################################
//#  Page Read through Direct Access  (0X0B)          
//#############################################
void user_flash_read_cmd(int fd)
{
     bank_addr = 0x00000000;
     uartm_wm_cmd(fd,0x30080004,bank_addr,1);
}

//# Flash Read Byte , addr : Address, exp_data: Expected Data, bcnt: valid byte cnt
struct s_result user_flash_read_compare(int fd,unsigned int addr, unsigned int exp_data, unsigned int bcnt)
{
    uint mask = 0x00;
    struct s_result result;
    result.flag = 0;
     // Check if there is change in bank address
    unsigned int new_bank = addr >> 16;
    if(bank_addr != new_bank) { 
     bank_addr = addr >> 16;
     printf("Changing Bank Address: %x\n",bank_addr);
     uartm_wm_cmd(fd,0x30080004,bank_addr,1);
    }
    result = uartm_rm_cmd(fd,addr);
    if (bcnt == 1) mask = 0x000000FF;
    else if (bcnt == 2) mask = 0x0000FFFF;
    else if (bcnt == 3) mask = 0x00FFFFFF;
    else if (bcnt == 4) mask = 0xFFFFFFFF;

    if ((exp_data & mask) == (result.value & mask)) {
        printf("Flash Read Addr: 0x%08x Data:0x%08x => Matched\n", addr, exp_data & mask);
        result.flag = 0;
    } else {
        printf("Flash Read Addr: 0x%08x Exp Data:0x%08x  Rxd Data:0x%08x => FAILED\n", addr, exp_data & mask, result.value & mask);
        result.flag = 1;
    }
    return result;
}



//##############################
//# Flash Read and Verify 
//##############################
void user_flash_verify(int fd,const char *file_path) {
   unsigned int addr,dataout,ncnt;
   int nbytes, total_bytes;
   char Instring[256];
   char substring[8];
   unsigned int tData = 0;
   nbytes = 0;
   total_bytes = 0;
   addr = 0;
   struct s_result result;
   unsigned int iErrCnt = 0;

   printf("User Flash Read back and verify Started\n");
   user_flash_read_cmd(fd);
        
   FILE* f_open;
   f_open = fopen(file_path, "r");

    dataout = 0x00;
    while (fgets(Instring,256, f_open)) {
        nbytes = num_word_per_line(Instring);
        //printf("Line:%d :%ld :word:%d : %s\n",nbytes,strlen(Instring), nbytes,Instring);
        if(Instring[0] == '@') {
            nbytes = 0; // Indicate this is address byte, not data byte
            dataout = 0;
            strncpy(substring,Instring+1,8);
            sscanf(substring,"%x",&addr);
            printf("setting address to 0x%08x\n",addr);
        } else {
            ncnt = 0;
            for(int i =0; i < nbytes; i++) {
               strncpy(substring,Instring+(i*3),2);
               substring[2] =0x00;
               //printf("SubString:%d:%d:%d: %s\n",nbytes,i,ncnt,substring);
               sscanf(substring,"%x",&tData);
               int tShift = (8 * i);
               dataout |= tData << tShift; 
               ncnt = ncnt + 1;
               total_bytes ++;
               if(ncnt == 4){
                   result = user_flash_read_compare(fd,addr,dataout,4);
                   iErrCnt +=result.flag;
                   addr = addr+4;
                   ncnt = 0;
                   dataout = 0x00;
                }
            }
            if(ncnt > 0 && ncnt < 4) {   // if line has less than 4 bytes
                 result = user_flash_read_compare(fd,addr,dataout,ncnt);
                 iErrCnt +=result.flag;
            }

            if (Instring[0] != '@' && Instring[0] != ' ' && nbytes >= 256) {
                 printf("addr 0x%08x: flash page write successful",addr);
                 if(nbytes > 256) {
                    printf("ERROR: *** Data over 256 hit");
                    exit(0);
                 } else {
                     nbytes =0;
                 }
            } 
        }
    }
    printf("total_bytes = %d\n",total_bytes);
    if(iErrCnt > 0) {
        printf("ERROR: Total Read compare failure %d detected \n",iErrCnt);
    }
    fclose(f_open);

}

 
 
int main(int argc, char *argv[] )
{

int  _serialPort;
struct s_result result;

  printf("runodude (Rev:0.5)- A Riscduino firmware downloading application");
  if( argc != 4 ) {
      //printf("Total Argument Received : %d \n",argc);
      printf("Format: %s <COM> <BaudRate> <Hex File>  \n", argv[0]);
      exit(0);
   } 
  //const char * device = "/dev/ttyACM0";
  const char *device = argv[1];
  uint32_t   baud_rate = atoi(argv[2]);
  const char *filename = argv[3];

  printf("COM PORT  = %s\n", device);
  printf("Baud Rate = %d\n", baud_rate);
  printf("Hex File  = %s\n", filename);
 
  _serialPort = open_serial_port(device, baud_rate);
  if (_serialPort < 0) { return 1; }

  user_reboot(_serialPort);
  user_chip_id(_serialPort);
  user_flash_device_id(_serialPort);
  user_flash_chip_erase(_serialPort);
  
  user_flash_progam(_serialPort,filename);
  user_flash_verify(_serialPort,filename);
 
  close(_serialPort);
  return 0;
}
