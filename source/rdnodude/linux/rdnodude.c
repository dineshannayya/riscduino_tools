// Uses POSIX serial port functions to send and receive data from a Jrk G2.
// NOTE: The Jrk's input mode must be "Serial / I2C / USB".
// NOTE: The Jrk's serial mode must be set to "USB dual port" if you are
//   connecting to it directly via USB.
// NODE: The Jrk's serial mode must be set to "UART" if you are connecting to
//   it via is TX and RX lines.
// NOTE: You might need to change the 'const char * device' line below to
//   specify the correct serial port.
 
#include <fcntl.h>
#include <stdio.h>
#include <unistd.h>
#include <stdint.h>
#include <termios.h>
 
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
  case 115200: cfsetospeed(&options, B115200); break;
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
      perror("failed to read from port");
      return -1;
    }
    if (r == 0)
    {
      // Timeout
      break;
    }
    received += r;
  }
  return received;
}
 
// Sets the target, returning 0 on success and -1 on failure.
//
// For more information about what this command does, see the "Set Target"
// command in the "Command reference" section of the Jrk G2 user's guide.
int jrk_set_target(int fd, uint16_t target)
{
  if (target > 4095) { target = 4095; }
  uint8_t command[2];
  command[0] = 0xC0 + (target & 0x1F);
  command[1] = (target >> 5) & 0x7F;
  return write_port(fd, command, sizeof(command));
}
 
// Gets one or more variables from the Jrk (without clearing them).
// Returns 0 for success, -1 for failure.
int jrk_get_variable(int fd, uint8_t offset, uint8_t * buffer, uint8_t length)
{
  uint8_t command[] = { 0xE5, offset, length };
  int result = write_port(fd, command, sizeof(command));
  if (result) { return -1; }
  ssize_t received = read_port(fd, buffer, length);
  if (received < 0) { return -1; }
  if (received != length)
  {
    fprintf(stderr, "read timeout: expected %u bytes, got %zu\n",
      length, received);
    return -1;
  }
  return 0;
}
 
// Gets the Target variable from the jrk or returns -1 on failure.
int jrk_get_target(int fd)
{
  uint8_t buffer[2];
  int result = jrk_get_variable(fd, 0x02, buffer, sizeof(buffer));
  if (result) { return -1; }
  return buffer[0] + 256 * buffer[1];
}
 
// Gets the Feedback variable from the jrk or returns -1 on failure.
int jrk_get_feedback(int fd)
{
  // 0x04 is the offset of the feedback variable in the "Variable reference"
  // section of the Jrk user's guide.  The variable is two bytes long.
  uint8_t buffer[2];
  int result = jrk_get_variable(fd, 0x04, buffer, sizeof(buffer));
  if (result) { return -1; }
  return buffer[0] + 256 * buffer[1];
}
 
int main()
{
  // Choose the serial port name.  If the Jrk is connected directly via USB,
  // you can run "jrk2cmd --cmd-port" to get the right name to use here.
  // Linux USB example:          "/dev/ttyACM0"  (see also: /dev/serial/by-id)
  // macOS USB example:          "/dev/cu.usbmodem001234562"
  // Cygwin example:             "/dev/ttyS7"
  const char * device = "/dev/ttyACM0";
 
  // Choose the baud rate (bits per second).  This does not matter if you are
  // connecting to the Jrk over USB.  If you are connecting via the TX and RX
  // lines, this should match the baud rate in the Jrk's serial settings.
  uint32_t baud_rate = 9600;
 
  int fd = open_serial_port(device, baud_rate);
  if (fd < 0) { return 1; }
 
  int feedback = jrk_get_feedback(fd);
  if (feedback < 0) { return 1; }
 
  printf("Feedback is %d.\n", feedback);
 
  int target = jrk_get_target(fd);
  if (target < 0) { return 1; }
  printf("Target is %d.\n", target);
 
  int new_target = (target < 2048) ? 2248 : 1848;
  printf("Setting target to %d.\n", new_target);
  int result = jrk_set_target(fd, new_target);
  if (result) { return 1; }
 
  close(fd);
  return 0;
}
