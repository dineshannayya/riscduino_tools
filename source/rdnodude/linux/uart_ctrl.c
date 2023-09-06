#include <fcntl.h>
#include <stdio.h>
#include <unistd.h>
#include <stdint.h>
#include <termios.h>
#include <stdlib.h>
#include <string.h>
#include <sys/ioctl.h>


int set_DTR(int fd, unsigned short level)
{
	int status;

	if (fd < 0) {
		perror("Set_DTR(): Invalid File descriptor");
		return -1;
	}

	if (ioctl(fd, TIOCMGET, &status) == -1) {
		perror("set_DTR(): TIOCMGET");
		return -1;
	}

	if (level) 
		status |= TIOCM_DTR;
	else 
		status &= ~TIOCM_DTR;

	if (ioctl(fd, TIOCMSET, &status) == -1) {
		perror("set_DTR(): TIOCMSET");
		return -1;
	}
	return 0;

}

int set_RTS(int fd, unsigned short level)
{
	int status;

	if (fd < 0) {
		perror("Invalid File descriptor");
		return -1;
	}

	if (ioctl(fd, TIOCMGET, &status) == -1) {
		perror("set_RTS(): TIOCMGET");
		return -1;
	}

	if (level) 
		status |= TIOCM_RTS;
	else 
		status &= ~TIOCM_RTS;

	if (ioctl(fd, TIOCMSET, &status) == -1) {
		perror("set_RTS(): TIOCMSET");
		return -1;
	}
	return 0;
}


#define SERIAL_DEVICE	"/dev/ttyUSB1"

main()
{
    int fd;
	int retval;
	int serial;

	fd = open(SERIAL_DEVICE, O_RDWR);
	if (fd < 0) {
		perror("Failed to open SERIAL_DEVICE");
		exit(1);
	}
	
	retval = ioctl(fd, TIOCMGET, &serial);
	if (retval < 0) {
		perror("ioctl() failed");
		exit(0);
	}
 
	if (serial & TIOCM_DTR)
		printf("%s:DTR is set\n", SERIAL_DEVICE);
	else
		printf("%s:DTR is not set\n", SERIAL_DEVICE);

	if (serial & TIOCM_LE)
		printf("%s:DSR is set\n", SERIAL_DEVICE);
	else
		printf("%s:DSR is not set\n", SERIAL_DEVICE);

	if (serial & TIOCM_RTS)
		printf("%s:RTS is set\n", SERIAL_DEVICE);
	else
		printf("%s:RTS is not set\n", SERIAL_DEVICE);

	if (serial & TIOCM_CTS)
		printf("%s:CTS is set\n", SERIAL_DEVICE);
	else
		printf("%s:CTS is not set\n", SERIAL_DEVICE);

     getchar();

	if (set_RTS(fd, 0)) {
		printf("%s: Failed to set RTS\n", SERIAL_DEVICE);
		exit(1);
	}
	if (set_DTR(fd, 0)) {
		printf("%s: Failed to set DTR\n", SERIAL_DEVICE);
		exit(1);
	}
	retval = ioctl(fd, TIOCMGET, &serial);
	if (retval < 0) {
		perror("ioctl() failed");
		exit(0);
	}
	if (serial & TIOCM_RTS)
		printf("%s:RTS is set\n", SERIAL_DEVICE);
	else
		printf("%s:RTS is not set\n", SERIAL_DEVICE);
	if (serial & TIOCM_DTR)
		printf("%s:DTR is set\n", SERIAL_DEVICE);
	else
		printf("%s:DTR is not set\n", SERIAL_DEVICE);
     getchar();
   close(fd);
}
