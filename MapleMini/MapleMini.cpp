// MapleMini.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"
#include "MapleMini.h"

void header()
{
	cout << rang::style::bold << "MapleMini Console Version 1.0\n" << endl;
	cout << rang::style::bold << "Built: " << __TIME__ << " " << __DATE__ << endl << endl;
}

void info()
{
	cout << rang::style::bold << "Command Line Options: " << "[] Required, {} Optional" << rang::style::reset << endl;
	cout << " Decypt: MapleMini.exe [\"c:\\Location\\to\\encrypted\\content\"]" << endl;
	cout << " Download: MapleMini.exe {-dl} [Title ID]" << endl << endl;

	cout << rang::style::bold << "Input Options: " << "[] Required, {} Optional" << rang::style::reset << endl;
	cout << " Download: [-dl] [\"Title ID\"]" << endl;
	cout << " Decrypt: [-de] [\"c:\\Location\\to\\encrypted\\content\"]" << endl << endl;

	cout << rang::style::bold << "----------------------------------------------------------" << endl << endl;
}

void simpleDecrypt(const char* path, char* defaultDir)
{
	_chdir(path);

	struct stat buffer;
	if ((stat("tmd", &buffer) == 0) && (stat("cetk", &buffer) == 0))
	{
		start(3, "tmd", "cetk");
		std::cout << "Content Decryption Complete.\n\n";
		_chdir(defaultDir);
		info();
	}
}

int main(signed int argc, char* argv[])
{
	rang::setControlMode(rang::control::Auto);
	SetConsoleTitleA("MapleSeed Command Line");

	//TitleInfo::CreateDatabase();

	auto defaultDir = (char*)argv[0];

	struct stat buffer;
	if ((stat("tmd", &buffer) == 0) && (stat("cetk", &buffer) == 0)) {
		simpleDecrypt("./", defaultDir);
		getchar();
		return 0;
	}

	header();
	info();

	while (true)
	{
		auto input = Toolbelt::getUserInput();
		auto _input = string(input);
		_input.replace(0, 4, "");

		std::string path;
		std::regex re("\\\"(.*)\\\"");
		std::smatch match;
		if (std::regex_search(input, match, re) && match.size() > 1) {
			path = match.str(1);
		}
		else path = std::string("");

		if (input[0] == '-' && input[1] == 'd' && input[2] == 'l' && _input.length() == 16)
		{
			string url = string("http://api.tsumes.com/title/" + _input);
			auto dc = DownloadClient(url.c_str());
			if (dc.error) continue;
			auto ti = TitleInfo(dc.dataBytes, (int)dc.length, ".");
			ti.DownloadContent();
			simpleDecrypt(ti.workingDir.c_str(), defaultDir);
			continue;
		}

		if (input[0] == '-' && input[1] == 'd' && input[2] == 'e' && path.length() > 2)
		{
			simpleDecrypt(Toolbelt::StringToCharArray(path), defaultDir);
			continue;
		}

		if (path.length() > 2 && Toolbelt::DirExists(Toolbelt::StringToCharArray(path)))
		{
			simpleDecrypt(Toolbelt::StringToCharArray(path), defaultDir);
			break;
		}

		if (strcmp(Toolbelt::StringToCharArray(input), "exit") == 0)
			exit(0);
	}
}

