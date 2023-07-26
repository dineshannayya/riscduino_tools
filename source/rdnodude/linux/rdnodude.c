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


struct s_result uartm_wm_cmd    (int fd,unsigned int addr, unsigned int data);
void            user_reboot     ();
int             open_serial_port(const char * device, uint32_t baud_rate);
int             write_port      (int fd, uint8_t * buffer, size_t size);
ssize_t         read_port       (int fd, uint8_t * buffer, size_t size);
void            flush_rx        (int fd);
void            flush_tx        (int fd);

//------------------------------------------------
// Flush Serial Port Rx Buffer
//------------------------------------------------
void flush_rx(int fd) {
   ioctl(fd, TCFLSH, 0); // flush receive
}

//------------------------------------------------
// Flush Serial Port Tx Buffer
//------------------------------------------------
void flush_tx(int fd) {
   ioctl(fd, TCFLSH, 1); // flush transmit
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

struct  s_result  ExtractReadResponse(char *buffer,int iSize,int iPos) {
     struct s_result result;
     result.flag = 0;

        char *InString = ByteArrayToString(buffer, iSize);

        char *delim = " ";
        unsigned count = 0;
        /* First call to strtok should be done with string and delimiter as first and second parameter*/
        char *token = strtok(InString,delim);
        count++;
        if(strcmp(token,"Response:")){
             /* Consecutive calls to the strtok should be with first parameter as NULL and second parameter as delimiter
              * * return value of the strtok will be the split string based on delimiter*/
             while(token != NULL) {
                     printf("Token no. %d : %s \n", count,token);
                     token = strtok(NULL,delim);
                     count++;
                     if(count == iPos) {
                      result.flag = 1;
                      break;
                     }
             }
            result.value = atoi(token);
        } else {
             printf("Invalid Response: %s Received", buffer);
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

     int iSize = read_port(fd,buffer, 14);
     result = ExtractReadResponse(buffer,iSize,2);

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
                //printf("%s",buffer);
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
            for(int i =0; i < 14; i++) {
               if(buffer[i] != wr_resp[i]) {
                 printf("Invalid Response: Received");
                 result.flag = 0;
               }
            }
            result.flag = 1;


            return result;
        }


//####################################################
//# Send wm <addr> <data> command and check the response
//#  Return: Response Good  => '1' 
//#  Return: Response Bad  => '0' 
//####################################################
        struct s_result uartm_wm_cmd(int fd,unsigned int addr, unsigned int data)
        {
            int retry;

            struct s_result result;
            result.flag = 0;
            result.value = 0;


            for (retry = 0; retry < 3; retry++)
            {
                flush_rx(fd);
                sprintf(buffer,"wm %08x %08x\n", addr, data);
                write_port(fd, buffer, 21);
                result= uartm_write_response(fd,addr,data);
                if (result.flag == 1) return result;
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
    uartm_wm_cmd(fd,0x30080000,0x80000000); // Set Bit[31] = 1 to indicate user flashing to caravel
    uartm_wm_cmd(fd,0x30080000,0x80000001);
    uartm_wm_cmd(fd,0x30080004,0x00001000);
    uartm_wm_cmd(fd,0x30020004,0x0000001F);
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
       printf("Exiting Serial port Loop due to Time Out");
      // Timeout
      break;
    }
    received += r;
  }
  return received;
}
 
 
 
int main(int argc, char *argv[] )
{

int  _serialPort;
struct s_result result;

  if( argc != 4 ) {
      printf("Total Argument Received : %d \n",argc);
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
  result = uartm_rm_cmd(_serialPort,0x30020000); //  # User Chip ID
  printf("Riscduino Chip ID:0x%08x ", result.value);
 

 
 
 
  close(_serialPort);
  return 0;
}
