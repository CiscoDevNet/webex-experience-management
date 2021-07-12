import sys
import subprocess
import os
import pkg_resources
import json
import zipfile
import datetime
import time
from pathlib import Path

Base_URL = ""
ConfigData = ""

def InstallDependenciesInTargetServer():
    try:
        requiredPackages = {'gitpython', 'requests', 'fabric3'}
        installedPackages = {pkg.key for pkg in pkg_resources.working_set}
        missing = requiredPackages - installedPackages
        if missing:
            print("These libraries by default are not installed in your system:")
            print(missing)
            print("Installing missing libraries..")
            python = sys.executable
            subprocess.check_call([python, '-m', 'pip', 'install', *missing], stdout=subprocess.DEVNULL)
            print('\nAll libraries installed')
        else:
            print('Seems like all required libraries are installed. Lets jump to Step2. :)\n');

    except Exception as ex:
        # Log exception
        print("Some dependencies could not be installed successfully. Exiting...")
        raise ex


def GetConfigValues():
    try:
        global ConfigData, Base_URL
        a_file = open("config.json", "r")
        ConfigData = json.load(a_file)
        a_file.close()
        if ConfigData['Certs']['IsProd']:
            Base_URL = 'https://' + ConfigData['Certs']['domain_name']
        else:
            Base_URL = 'http://' + ConfigData['Host']
    except Exception as ex:
        print("Error in config.json file. Please check your configurations.")
        raise ex

def InstallDotnetInTargetServer():
    try:
        global ConfigData
        from fabric.api import env, run
        env.host_string = ConfigData['Host']
        env.user = ConfigData['Username']
        env.port = ConfigData['Port']
        env.key_filename = ConfigData['Key_Filename']
        print('Logged in to server where deployment will be done.')
        try:
            run('dotnet --version')
            print("Dotnet package already installed, so skipping installation")
        except:
            print("Seems like dotnet is not installed in the server. Gonna install it now")

            try:
                time.sleep(2)
                run('wget https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb')
                run('sudo dpkg -I packages-microsoft-prod.deb')
                run('sudo add-apt-repository universe')
                run('sudo apt-get update')
                run('sudo apt-get install apt-transport-https')
                run('sudo apt-get install dotnet-sdk-3.1')
                run('sudo apt-get install aspnetcore-runtime-3.1')
                print("All done with dotnet installation now")
            except:
                print('Something failed while installing dotnet. No Worries...Running troubleshooting steps.')
                time.sleep(2)
                try:
                    run('sudo dpkg --purge packages-microsoft-prod && sudo dpkg -i packages-microsoft-prod.deb')
                    run('sudo apt-get update')
                    run('sudo apt-get install dotnet-sdk-3.1')
                    run('sudo apt-get install aspnetcore-runtime-3.1')
                    run('dotnet --info')
                    print("Everything should be fine now with dotnet installation")
                except Exception as ex:
                    print('Unfortunately troubleshooting steps also failed. Please check with WXM team for further support on this.\n')
                    raise ex
    except Exception as ex:
        print('Got this error with dotnet installation which was unexpected. Please check with WXM team for further support on this.\n')
        raise ex

def InstallNginxInTargetServer():
    try:
        global ConfigData
        from fabric.api import env, run
        env.host_string = ConfigData['Host']
        env.user = ConfigData['Username']
        env.port = ConfigData['Port']
        env.key_filename = ConfigData['Key_Filename']
        try:
            run('nginx -v')
            print("Nginx is already installed.\n")
        except:
            print ("Installing Nginx.....\n")
            #run('sudo apt-get update')
            run('sudo apt install nginx-full')
            print("\nNginx installation completed\n")
        print("Starting Nginx service now\n")
        run('sudo service nginx start')
    except Exception as ex:
        print('Something unexpected happened with Nginx installation and it failed. Please check with WXM team for further support on this.\n')
        raise ex

def ServiceSetupInTargetServer():
    try:
        global ConfigData
        from fabric.api import env, run
        from fabric.context_managers import cd
        env.host_string = ConfigData['Host']
        env.user = ConfigData['Username']
        env.port = ConfigData['Port']
        env.key_filename = ConfigData['Key_Filename']
        service_name = ConfigData['Service_Name']
        username = ConfigData['Username']
        print('Creating systemctl script for ' + service_name)
        content_service = '''"[Unit]\nDescription=Invitations API controllers (ASP.NET Core) running on ubuntu 18.04\n[Service]\nWorkingDirectory=/var/www/''' + 'inv-bin-' + service_name +'''\nExecStart=/usr/bin/dotnet /var/www/'''+ 'inv-bin-' + service_name +'''/XM.ID.Invitations.API.dll\nRestart=always\n# Restart service after 2 seconds if the dotnet service crashes:\nRestartSec=2\nKillSignal=SIGINT\nSyslogIdentifier=dotnet-invitations-''' + service_name + '''\nUser=*|User|*\nEnvironment=ASPNETCORE_ENVIRONMENT=Development\nEnvironment=DOTNET_PRINT_TELEMETRY_MESSAGE=false\n[Install]\nWantedBy=multi-user.target"'''
        with cd('/var/www/'):
            run('sudo mkdir -p dummy_build')
            run('sudo chown ' + username + ':' + username + ' dummy_build/')
            #run('sudo rm -rf ' + service_name)
            run('sudo ln -s dummy_build ' + 'inv-bin-' + service_name)
            run('sudo chown ' + username + ':' + username + ' inv-bin-' + service_name)
            run('ls -lrt')
        content_service = content_service.replace("*|User|*", username)
        cmd = r"sudo sh -c 'echo " + content_service + " > /etc/systemd/system/" + 'kestrel-inv-bin-' + service_name + ".service'"
        run(cmd)

        print('script created with name ' + 'kestrel-inv-bin-' + service_name + '.service')
        print('Enabling this script now...')
        time.sleep(2);
        run('sudo systemctl enable ' + 'kestrel-inv-bin-' + service_name + '.service')
        run('dotnet dev-certs https')
    except Exception as ex:
        print("Something unexpected happened with Systemctl script creation. Please check with WXM team for further support on this.\n")
        raise ex

def DownloadInvitationCode():
    try:
        import git
        print ("Going to start cloning of Invitations Delivery repository.\n")
        isCode = os.path.isdir('WXM-Invitations')
        if isCode:
            time.sleep(1)
            print('Looks like the source code folder already exists here. Skipping cloning...\n')
        else:
            git.Git().clone("https://github.com/cisco/WXM-Invitations.git")
        print ("All code from Invitations Delivery repository is cloned successfully.\n")
    except Exception as ex:
        print("Something unexpected happened while cloning the repository. Please check with WXM team for further support on this.\n")
        raise ex

def UpdateAppSettings():
    try:
        print("Updating the appsettings.json file with MongoDB connection string\n")
        time.sleep(1)
        global ConfigData
        cwd = os.getcwd()
        appsetting_path = Path(cwd + "/WXM-Invitations/XM.ID.Invitations.API/XM.ID.Invitations.API/bin/Release/netcoreapp3.1/ubuntu.18.04-x64/publish/appsettings.json")
        a_file = open(appsetting_path, "r")

        data = json.load(a_file)
        a_file.close()
        data['MONGODB_URL'] = ConfigData['MongoDB']['MongoDB_Connection']
        data['DbNAME'] = ConfigData['MongoDB']['MongoDB_Database']
        appsetting_path = Path(cwd + "/WXM-Invitations/XM.ID.Invitations.API/XM.ID.Invitations.API/bin/Release/netcoreapp3.1/ubuntu.18.04-x64/publish/appsettings.json")
        a_file = open(appsetting_path, "w")

        json.dump(data, a_file, indent = 2)
        a_file.close()
        print("appsettings.json is updated successfully\n")
    except Exception as ex:
        print("An unexpected error occurred while updating the appsettings.json of Invitations API. Please check with WXM team for further support on this.\n")
        raise ex

def UpdateNotificationSettings():
    try:
        global ConfigData, Base_URL
        cwd = os.getcwd()
        appsetting_path = Path(cwd + "/WXM-Invitations/XM.ID.Invitations.Notifications/XM.ID.Invitations.Notifications/bin/Release/netcoreapp3.1/ubuntu.18.04-x64/publish/appsettings.json")
        a_file = open(appsetting_path, "r")
        data = json.load(a_file)
        a_file.close()
        data['DBConnectionString'] = ConfigData['MongoDB']['MongoDB_Connection']
        data['DBName'] = ConfigData['MongoDB']['MongoDB_Database']
        data['ApplicationLogpath'] = '/var/www/SystemLog/Notification/' + ConfigData['Service_Name']
        data['PathToEmail'] = Base_URL + "/files-noti-" + ConfigData['Service_Name']
        data['LogFilePath'] = "/var/www/files-noti-" + ConfigData['Service_Name']
        a_file = open(appsetting_path, "w")
        json.dump(data, a_file, indent = 2)
        a_file.close()
    except Exception as ex:
        print("An unexpected error occurred while updating the appsettings.json of Notifications. Please check with WXM team for further support on this.\n")
        raise ex

def UpdateReportsSettings():
    try:
        global ConfigData, Base_URL
        cwd = os.getcwd()
        appsetting_path = Path(cwd + "/WXM-Invitations/DPReporting/DPReporting/bin/Release/netcoreapp3.1/ubuntu.18.04-x64/publish/appsettings.json")
        a_file = open(appsetting_path, "r")
        data = json.load(a_file)
        a_file.close()
        data['MONGODB_URL'] = ConfigData['MongoDB']['MongoDB_Connection']
        data['DbNAME'] = ConfigData['MongoDB']['MongoDB_Database']
        data['LogFilePath'] = '/var/www/SystemLog/DPReport/' + ConfigData['Service_Name'] 
        data['DetailedLogs']['PathToEmail'] = Base_URL + '/files-reports-' + ConfigData['Service_Name']
        data['DetailedLogs']['ReportPath'] = '/var/www/files-reports-' +  ConfigData['Service_Name']
        a_file = open(appsetting_path, "w")
        json.dump(data, a_file, indent = 2)
        a_file.close()
    except Exception as ex:
        print("An unexpected error occurred while updating the appsettings.json of Reports. Please check with WXM team for further support on this.\n")
        raise ex

def UpdateDataMergerSettings():
    try:
        global ConfigData, Base_URL
        cwd = os.getcwd()
        appsetting_path = Path(cwd + "/WXM-Invitations/WXMInvitationsDataMerger/WXMInvitationsDataMerger/bin/Release/netcoreapp3.1/ubuntu.18.04-x64/publish/appsettings.json")
        a_file = open(appsetting_path, "r")
        data = json.load(a_file)
        a_file.close()
        data['MONGODB_URL'] = ConfigData['MongoDB']['MongoDB_Connection']
        data['DbNAME'] = ConfigData['MongoDB']['MongoDB_Database']
        data['LogFilePath'] = '/var/www/SystemLog/DataMerger/' + ConfigData['Service_Name']
        a_file = open(appsetting_path, "w")
        json.dump(data, a_file, indent = 2)
        a_file.close()
    except Exception as ex:
        print("An unexpected error occurred while updating the appsettings.json of Data Merger component. Please check with WXM team for further support on this.\n")
        raise ex

def UpdateSFTPS3Settings():
    try:
        global ConfigData, Base_URL
        cwd = os.getcwd()
        appsetting_path = Path(cwd + "/WXM-Invitations/SFTPToS3Sync/SFTPToS3Sync/bin/Release/netcoreapp3.1/ubuntu.18.04-x64/publish/appsettings.json")
        a_file = open(appsetting_path, "r")
        data = json.load(a_file)
        a_file.close()
        data['MongoDB']['ConnectionString'] = ConfigData['MongoDB']['MongoDB_Connection']
        data['MongoDB']['DataBaseName'] = ConfigData['MongoDB']['MongoDB_Database']
        data['LogFilePath'] = '/var/www/SystemLog/SFTPS3Sync/' + ConfigData['Service_Name']
        data['S3']['BucketName'] = ConfigData['SftpS3']['BucketName']
        data['S3']['AWSAccessKeyId'] = ConfigData['SftpS3']['AWSAccessKeyId']
        data['S3']['AWSSecretAccessKey'] = ConfigData['SftpS3']['AWSSecretAccessKey']
        data['S3']['BucketRegionCode'] = ConfigData['SftpS3']['BucketRegionCode']
        data['S3']['BaseDirectory'] = ConfigData['SftpS3']['BaseDirectory']
        data['S3']['IsServerDeployment'] = ConfigData['SftpS3']['IsServerDeployment']
        data['SFTP']['BaseDirectory'] = ConfigData['SftpS3']['SFTPBaseDirectory']
        data['SFTP']['Url'] = ConfigData['SftpS3']['SFTPUrl']
        data['SFTP']['Port'] = ConfigData['SftpS3']['SFTPPort']
        data['SFTP']['Username'] = ConfigData['SftpS3']['SFTPUsername']
        data['SFTP']['Password'] = ConfigData['SftpS3']['SFTPPassword']
     
        a_file = open(appsetting_path, "w")
        json.dump(data, a_file, indent = 2)
        a_file.close()
    except Exception as ex:
        print("An unexpected error occurred while updating the appsettings.json of Reports. Please check with WXM team for further support on this.\n")
        raise ex


def PublishBuild(projectPath):
    try:
        global ConfigData
        if sys.platform.startswith('win'):
            # Win-specific code here...
            print("Windows system detected. So publishing the build using windows dotnet SDK\n")
            dotnetzipcheck = os.path.exists("Dotnet")
            if not dotnetzipcheck:
                print("Dotnet package not found, so downloading it. It might take few mins to download and install.\n");
                import requests
                url = ConfigData['DotnetDownloadLink']['Windows']
                req = requests.get(url, allow_redirects=True)
                print("Downloaded successfully. Now installing it.\n")
                with open("Dotnet.zip", "wb") as code:
                    code.write(req.content)
                with zipfile.ZipFile("Dotnet.zip", "r") as zip_ref:
                    zip_ref.extractall("Dotnet")
                    code.close()
                    zip_ref.close()
                    os.remove("Dotnet.zip")

            print("Dotnet folder found on this system. Going to publish executables.\n")
            cwd = os.getcwd()
            dotnetDir = cwd + '\\Dotnet'

            os.chdir(Path(cwd + projectPath))
            dotnetExePath = Path(dotnetDir) / "dotnet.exe"
            publish_res = os.system('"' + str(dotnetExePath) + '"' + ' publish -c Release -r ubuntu.18.04-x64')

        elif sys.platform.startswith('linux'):
            # Linux-specific code here...
            print ("Linux system not supported.")
        elif sys.platform.startswith('darwin'):
            # Mac OS specific code.
            print ("MacOS system detected. So publishing the build using windows dotnet SDK\n")
            dotnetzipcheck = os.path.exists("dotnet-sdk-3.1.302-osx-x64.tar.gz")
            if not dotnetzipcheck:
                print("Dotnet package not found, so downloading it. It might take few mins to download and install.\n")
                import requests
                url = ConfigData['DotnetDownloadLink']['MacOS']
                req = os.system('curl -O ' + url)
                os.system(r'mkdir -p "$HOME/dotnet" && tar zxf dotnet-sdk-3.1.302-osx-x64.tar.gz -C "$HOME/dotnet"')
                os.system("export DOTNET_ROOT=$HOME/dotnet")
                os.system("export PATH=$PATH:$HOME/dotnet")
                #os.remove("dotnet-sdk-3.1.302-osx-x64.tar.gz")

            os.system("dotnet --info")

            print("Dotnet folder found on this system. Going to publish executables.\n")
            cwd = os.getcwd()
            os.chdir(Path(cwd + projectPath))
            publish_res = os.system('dotnet publish -c Release -r ubuntu.18.04-x64')


        #Common code for all type of OS
        if publish_res == 0:
            print("Great News. Publish of executables is successful.")
        else:
            print("Publish of executables is unsuccessful. Please check with WXM team for further support on this.\n")
            raise

        os.chdir(cwd)

    except Exception as ex:
        print("Error occured.")
        raise ex

def DeloyBuildOnServer(publishPath, publishzip_name, linuxfoldername, service_name):
    try:
        global ConfigData
        cwd = os.getcwd()
        os.chdir(Path(cwd + publishPath))
        publishZip = zipfile.ZipFile(publishzip_name, 'w', zipfile.ZIP_DEFLATED)
        zipdir('./publish', publishZip)
        publishZip.close()
        from fabric.api import env, run, put
        from fabric.context_managers import cd
        env.host_string = ConfigData['Host']
        env.user = ConfigData['Username']
        env.port = ConfigData['Port']
        username = ConfigData['Username']
        env.key_filename = ConfigData['Key_Filename']
        put(publishzip_name, '/home/' + username + '/')
        run('sudo apt-get install unzip')
        run('sudo unzip ' + publishzip_name)
        movecmd = 'sudo mv -f publish ' + linuxfoldername + str(datetime.datetime.utcnow())[0:10] + '-' + service_name
        run(movecmd)
        run('sudo rm ' + publishzip_name)
        run('sudo mv -f ' + linuxfoldername + str(datetime.datetime.utcnow())[0:10] + '-' + service_name + ' /var/www/')
        with cd('/var/www/'):
            run('sudo unlink ' + linuxfoldername + service_name)
            run('sudo ln -s ' + linuxfoldername + str(datetime.datetime.utcnow())[0:10] + '-' + service_name + ' ' + linuxfoldername + service_name)
            run('sudo chown ' + username + ':' + username + ' ' + linuxfoldername + service_name)
            run('sudo chown ' + username + ':' + username + ' ' + linuxfoldername + service_name +'/*')
            run('if [ -d "dummy_build" ]; then sudo rm -Rf dummy_build; fi')
            run('if [ -d "dummy_notifications" ]; then sudo rm -Rf dummy_notifications; fi')
            run('if [ -d "dummy_reports" ]; then sudo rm -Rf dummy_reports; fi')
            run('if [ -d "dummy_datamerger" ]; then sudo rm -Rf dummy_datamerger; fi')
            run('if [ -d "dummy_sftps3" ]; then sudo rm -Rf dummy_sftps3; fi')
            run('sudo systemctl start ' + 'kestrel-' +  linuxfoldername + service_name + '.service')
        os.chdir(cwd)
    except Exception as ex:
        print("Error has occured.")
        raise ex

def zipdir(path, ziph):
    # ziph is zipfile handle
    for root, dirs, files in os.walk(path):
        for file in files:
            ziph.write(os.path.join(root, file))

def SetupNginx():
    try:
        global ConfigData
        from fabric.api import env, run, put, settings
        from fabric.context_managers import cd
        env.host_string = ConfigData['Host']
        env.user = ConfigData['Username']
        username = ConfigData['Username']
        env.port = ConfigData['Port']
        env.key_filename = ConfigData['Key_Filename']
        service_name = ConfigData['Service_Name']
        isprod = ConfigData['Certs']['IsProd']
        notificationLogUser = ConfigData['NotificationLogAuthentication']['Username']
        notificationPassword = ConfigData['NotificationLogAuthentication']['Password']
        isNotification = ConfigData['NotificationAndReportModule']['InstallNotificationAndReportModule']
        proxy_co = '''proxy_redirect	off;\nproxy_set_header 	Host $host;\nproxy_set_header	X-Real-IP $remote_addr;\nproxy_set_header	X-Forwarded-For $proxy_add_x_forwarded_for;\nproxy_set_header	X-Forwarded-Proto $scheme;\nclient_max_body_size	10m;\nclient_body_buffer_size	128k;\nproxy_connect_timeout	90;\nproxy_send_timeout	90;\nproxy_read_timeout	90;\nproxy_buffers	32 4k;'''
        if isNotification:
            try:
                run('htpasswd --version')
                print ('Basic auth util is already installed')
            except:
                print('Basic auth util is not installed. Installing')
                with settings(prompts={'Do you want to continue? [Y/n] ': 'Y' }):
                    run ('sudo apt-get install apache2-utils')
            run('sudo mkdir -p /etc/apache2')
            with settings(prompts={'New password: ': notificationPassword }):
                run('sudo htpasswd -c /etc/apache2/.htpasswd ' + notificationLogUser)
        with cd('/etc/nginx/'):
            cmd = r"echo '" + proxy_co + r"' | sudo tee proxy.conf"
            run(cmd)
        nginx_conf_nonprod = '''events {\n    worker_connections 768;\n    multi_accept on;\n}\nhttp {\n	include        /etc/nginx/proxy.conf;\n    server_tokens  off;\n    # MIME\n    include     mime.types;\n    default_type application/octet-stream;\n    sendfile on;\n    keepalive_timeout   39; # Adjust to the lowest possible value that makes sense for your use case.\n    client_body_timeout 20; client_header_timeout 10; send_timeout 10;\n    upstream hellomvc_''' + service_name + '''{\n        server localhost:6000;\n    }\n    server {\n		listen  *:80;\n		root /var/www/html;\n\n		location / {\n			try_files $uri $uri/ =404;\n		}\n		#Redirects all traffic\n		location /''' + service_name + ''' {\n			rewrite /''' + service_name + '''/(.*) /$1 break;\n			proxy_pass http://hellomvc_''' + service_name + ''';\n		}\n	}\n}'''
        nginx_conf_prod = '''events {\n	worker_connections 768;\n    multi_accept on;\n}\nhttp {\n	include        /etc/nginx/proxy.conf;\n    server_tokens  off;\n    # MIME\n    include     mime.types;\n    default_type application/octet-stream;\n    sendfile on;\n    keepalive_timeout   39; # Adjust to the lowest possible value that makes sense for your use case.\n    client_body_timeout 20; client_header_timeout 10; send_timeout 10;\n    upstream hellomvc_''' + service_name + '''{\n        server localhost:6000;\n    }\n    server {\n            listen                    *:443 ssl;\n            root /var/www/html;\n\n\n            server_name               *|ServerName|*;\n            ssl_certificate           /etc/ssl/certs/*|Certificate|*;\n            ssl_certificate_key       /etc/ssl/certs/*|CertificateKey|*;\n\n            ssl_protocols             TLSv1.2 TLSv1.3;\n            ssl_prefer_server_ciphers on;\n            ssl_ciphers "ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-CHACHA20-POLY1305:ECDHE-RSA-CHACHA20-POLY1305:ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-SHA384:ECDHE-RSA-AES256-SHA384:ECDHE-ECDSA-AES128-SHA256:ECDHE-RSA-AES128-SHA256";\n            ssl_ecdh_curve            secp384r1;\n            ssl_session_cache         shared:SSL:10m;\n            ssl_session_tickets       off;\n            #ssl_stapling              on; #ensure your cert is capable\n            ssl_stapling_verify       on; #ensure your cert is capable\n            add_header Strict-Transport-Security "max-age=63072000; includeSubdomains; preload";\n            add_header X-Frame-Options "SAMEORIGIN";\n            add_header X-Content-Type-Options nosniff;\n            location / {\n                try_files $uri $uri/ =404;\n            }\n            #Redirects all traffic\n            location /''' + service_name + ''' {\n			rewrite /''' + service_name + '''/(.*) /$1 break;\n			proxy_pass http://hellomvc_''' + service_name + ''';\n            }\n        }\n}'''
        nginx_conf_notification_nonprod = '''events {\n    worker_connections 768;\n    multi_accept on;\n}\nhttp {\n	include        /etc/nginx/proxy.conf;\n    server_tokens  off;\n    # MIME\n    include     mime.types;\n    default_type application/octet-stream;\n    sendfile on;\n    keepalive_timeout   39; # Adjust to the lowest possible value that makes sense for your use case.\n    client_body_timeout 20; client_header_timeout 10; send_timeout 10;\n    upstream hellomvc_''' + service_name + '''{\n        server localhost:6000;\n    }\n    server {\n		listen  *:80;\n		root /var/www/html;\n\n		location / {\n			try_files $uri $uri/ =404;\n		}\n		#Redirects all traffic\n		location /''' + service_name + ''' {\n			rewrite /''' + service_name + '''/(.*) /$1 break;\n			proxy_pass http://hellomvc_''' + service_name + ';\n            }\n            #Redirects all logs traffic and enable basic auth\n            location /files-noti-' + service_name + ' {\n                auth_basic              "Restricted Access!";\n                auth_basic_user_file    /etc/apache2/.htpasswd;\n                root /var/www/;\n                try_files $uri $uri/ =404;\n		}\n	location /files-reports-' + service_name + ' {\n		auth_basic "Restricted Access!";\n	auth_basic_user_file /etc/apache2/.htpasswd; \n	root /var/www/;\n	try_files $uri $uri/ /error_mess.html;\n	}\n		location /error_mess.html {\n	return 404 "The file you are trying to download is no longer available and this link has expired.";\n	}\n}\n}'
        nginx_conf_notification_prod = '''events {\n	worker_connections 768;\n    multi_accept on;\n}\nhttp {\n	include        /etc/nginx/proxy.conf;\n    server_tokens  off;\n    # MIME\n    include     mime.types;\n    default_type application/octet-stream;\n    sendfile on;\n    keepalive_timeout   39; # Adjust to the lowest possible value that makes sense for your use case.\n    client_body_timeout 20; client_header_timeout 10; send_timeout 10;\n    upstream hellomvc_''' + service_name + '''{\n        server localhost:6000;\n    }\n    server {\n            listen                    *:443 ssl;\n            root /var/www/html;\n\n\n            server_name               *|ServerName|*;\n            ssl_certificate           /etc/ssl/certs/*|Certificate|*;\n            ssl_certificate_key       /etc/ssl/certs/*|CertificateKey|*;\n\n            ssl_protocols             TLSv1.2 TLSv1.3;\n            ssl_prefer_server_ciphers on;\n            ssl_ciphers "ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-CHACHA20-POLY1305:ECDHE-RSA-CHACHA20-POLY1305:ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-SHA384:ECDHE-RSA-AES256-SHA384:ECDHE-ECDSA-AES128-SHA256:ECDHE-RSA-AES128-SHA256";\n            ssl_ecdh_curve            secp384r1;\n            ssl_session_cache         shared:SSL:10m;\n            ssl_session_tickets       off;\n            #ssl_stapling              on; #ensure your cert is capable\n            ssl_stapling_verify       on; #ensure your cert is capable\n            add_header Strict-Transport-Security "max-age=63072000; includeSubdomains; preload";\n            add_header X-Frame-Options "SAMEORIGIN";\n            add_header X-Content-Type-Options nosniff;\n            location / {\n                try_files $uri $uri/ =404;\n            }\n            #Redirects all traffic\n            location /''' + service_name + ''' {\n         rewrite /''' + service_name + '''/(.*) /$1 break;\n         proxy_pass http://hellomvc_''' + service_name + ''';\n          }\n            #Redirects all logs traffic and enable basic auth\n            location /files-noti-''' + service_name + ''' {\n                auth_basic              "Restricted Access!";\n               auth_basic_user_file    /etc/apache2/.htpasswd;\n                root /var/www/;\n                try_files $uri $uri/ =404;\n            }\n         location /files-reports-''' + service_name + ''' {\n              auth_basic "Restricted Access!";\n              auth_basic_user_file /etc/apache2/.htpasswd; \n              root /var/www/;\n              try_files $uri $uri/ /error_mess.html;\n        }\n         location /error_mess.html {\n               return 404 "The file you are trying to download is no longer available and this link has expired.";\n	}\n    }\n}'''
        if isprod:
            print("Do prod steps")
            key_path = ConfigData['Certs']['key_path']
            cert_path = ConfigData['Certs']['certificate_path']
            servername = ConfigData['Certs']['domain_name']
            put(key_path, '/home/' + username + '/')
            put(cert_path, '/home/' + username + '/')
            import ntpath
            key_name = ntpath.basename(key_path)
            cert_name = ntpath.basename(cert_path)
            run('sudo mv ' + key_name +  ' /etc/ssl/certs')
            run('sudo mv ' + cert_name +  ' /etc/ssl/certs')
            nginx_conf_prod = nginx_conf_prod.replace('*|Certificate|*', cert_name)
            nginx_conf_prod = nginx_conf_prod.replace('*|ServerName|*', servername)
            nginx_conf_prod = nginx_conf_prod.replace('*|CertificateKey|*', key_name)

            nginx_conf_notification_prod = nginx_conf_notification_prod.replace('*|Certificate|*', cert_name)
            nginx_conf_notification_prod = nginx_conf_notification_prod.replace('*|ServerName|*', servername)
            nginx_conf_notification_prod = nginx_conf_notification_prod.replace('*|CertificateKey|*', key_name)
            if isNotification:
                cmd = r"echo '" + nginx_conf_notification_prod + r"' | sudo tee /etc/nginx/nginx.conf"
            else:
                cmd = r"echo '" + nginx_conf_prod + r"' | sudo tee /etc/nginx/nginx.conf"
            run(cmd)
        else:
            if isNotification:
                cmd = r"echo '" + nginx_conf_notification_nonprod + r"' | sudo tee /etc/nginx/nginx.conf"
            else:
                cmd = r"echo '" + nginx_conf_nonprod + r"' | sudo tee /etc/nginx/nginx.conf"
            run(cmd)
        run('sudo nginx -t')
        run('sudo nginx -s reload')
    except Exception as ex:
        print("Error occured.")
        raise ex

def InstallACMModule():
    try:
        global ConfigData, Base_URL
        service_name = ConfigData['Service_Name']
        cwd = os.getcwd()
        import json
        mainJS_path = Path(cwd + "/WXM-Invitations/ACM-Frontend/js/main.js")
        with open(mainJS_path,'r') as f:
            lines = f.readlines()

        with open(mainJS_path,'w') as f:
            for line in lines:
                if line.find('{DispatchAPI_Base_URL}') != -1:
                    line = line.replace('{DispatchAPI_Base_URL}', Base_URL + '/' + service_name)
                f.write(line)
        os.chdir(Path(cwd + '/WXM-Invitations'))
        publishZip = zipfile.ZipFile('ACM-Frontend.zip', 'w', zipfile.ZIP_DEFLATED)
        zipdir('./ACM-Frontend', publishZip)
        publishZip.close()
        from fabric.api import env, run, put, settings
        from fabric.context_managers import cd
        env.host_string = ConfigData['Host']
        env.user = ConfigData['Username']
        env.port = ConfigData['Port']
        username = ConfigData['Username']
        env.key_filename = ConfigData['Key_Filename']
        put('ACM-Frontend.zip', '/home/' + username + '/')
        os.chdir(cwd)
        run('sudo apt-get install unzip')
        run('sudo unzip ACM-Frontend.zip')
        movecmd = 'sudo mv -f ACM-Frontend ' + service_name
        run(movecmd)
        run('sudo rm ACM-Frontend.zip')
        run('sudo mkdir -p /var/www/html/acm/')
        run('sudo mv -f ' + service_name + ' /var/www/html/acm/' + service_name)
        mainJS_path = Path(cwd + "/WXM-Invitations/ACM-Frontend/js/main.js")
        with open(mainJS_path,'r') as f:
            lines = f.readlines()

        with open(mainJS_path,'w') as f:
            for line in lines:
                if line.find(Base_URL + '/' + service_name) != -1:
                    line = line.replace(Base_URL + '/' + service_name, '{DispatchAPI_Base_URL}')
                f.write(line)
    except Exception as ex:
        print("Error ocurred while installing ACM module.")
        raise ex
    
def ReportModuleSetup():
    try:
        global ConfigData
        from fabric.api import env, run
        from fabric.context_managers import cd
        env.host_string = ConfigData['Host']
        env.user = ConfigData['Username']
        env.port = ConfigData['Port']
        env.key_filename = ConfigData['Key_Filename']
        service_name = ConfigData['Service_Name']
        username = ConfigData['Username']
        print('Creating report systemctl script for ' + service_name)
        content_service = '''"[Unit]\nDescription=Reports build (.NET Core) running on ubuntu 18.04\n[Service]\nWorkingDirectory=/var/www/''' + 'reports-bin-' + service_name + '''\nExecStart=/usr/bin/dotnet /var/www/''' + 'reports-bin-' + service_name + '''/DPReporting.dll\nRestart=always\n# Restart service after 2 seconds if the dotnet service crashes:\nRestartSec=2\nKillSignal=SIGINT\nSyslogIdentifier=dotnet-reports-''' + service_name + '''\nUser=*|User|*\nEnvironment=ASPNETCORE_ENVIRONMENT=Development\nEnvironment=DOTNET_PRINT_TELEMETRY_MESSAGE=false\n[Install]\nWantedBy=multi-user.target"'''
        with cd('/var/www/'):
            run('sudo mkdir -p dummy_reports')
            run('sudo chown ' + username + ':' + username + ' dummy_reports/')
            run('sudo rm -rf ' + 'reports-bin-' + service_name)
            run('sudo ln -s dummy_reports/ ' + 'reports-bin-' + service_name)
            run('sudo chown ' + username + ':' + username + ' reports-bin-' + service_name)
            run('ls -lrt')
            run('sudo mkdir -p SystemLog/DPReport/' + service_name)
            run('sudo mkdir -p files-reports-' + service_name)
            run('sudo chown ' + username + ':' + username + ' files-reports-' + service_name + '/')
        content_service = content_service.replace("*|User|*", username)
        cmd = r"sudo sh -c 'echo " + content_service + " > /etc/systemd/system/" + 'kestrel-reports-bin-' + service_name + ".service'"
        run(cmd)
        run('sudo systemctl enable ' + 'kestrel-reports-bin-' + service_name + '.service')
    except Exception as ex:
        print("Reports Service setup failed")
        raise ex

def ReportDataMergerModuleSetup():
    try:
        global ConfigData
        from fabric.api import env, run
        from fabric.context_managers import cd
        env.host_string = ConfigData['Host']
        env.user = ConfigData['Username']
        env.port = ConfigData['Port']
        env.key_filename = ConfigData['Key_Filename']
        service_name = ConfigData['Service_Name']
        username = ConfigData['Username']
        print('Creating report systemctl script for ' + service_name)
        content_service = '''"[Unit]\nDescription=Reports build (.NET Core) running on ubuntu 18.04\n[Service]\nWorkingDirectory=/var/www/''' + 'datamerger-bin-' + service_name + '''\nExecStart=/usr/bin/dotnet /var/www/''' + 'datamerger-bin-' + service_name + '''/WXMInvitationsDataMerger.dll\nRestart=always\n# Restart service after 2 seconds if the dotnet service crashes:\nRestartSec=2\nKillSignal=SIGINT\nSyslogIdentifier=dotnet-datamerger-''' + service_name + '''\nUser=*|User|*\nEnvironment=ASPNETCORE_ENVIRONMENT=Development\nEnvironment=DOTNET_PRINT_TELEMETRY_MESSAGE=false\n[Install]\nWantedBy=multi-user.target"'''
        with cd('/var/www/'):
            run('sudo mkdir -p dummy_datamerger')
            run('sudo chown ' + username + ':' + username + ' dummy_datamerger/')
            run('sudo rm -rf ' + 'datamerger-bin-' + service_name)
            run('sudo ln -s dummy_datamerger/ ' + 'datamerger-bin-' + service_name)
            run('sudo chown ' + username + ':' + username + ' datamerger-bin-' + service_name)
            run('ls -lrt')
            run('sudo mkdir -p SystemLog/DataMerger/' + service_name)
        content_service = content_service.replace("*|User|*", username)
        cmd = r"sudo sh -c 'echo " + content_service + " > /etc/systemd/system/" + 'kestrel-datamerger-bin-' + service_name + ".service'"
        run(cmd)
        run('sudo systemctl enable ' + 'kestrel-datamerger-bin-' + service_name + '.service')
    except Exception as ex:
        print("Reports Service setup failed")
        raise ex

def NotificationModuleSetup():
    try:
        global ConfigData
        from fabric.api import env, run
        from fabric.context_managers import cd
        env.host_string = ConfigData['Host']
        env.user = ConfigData['Username']
        env.port = ConfigData['Port']
        env.key_filename = ConfigData['Key_Filename']
        service_name = ConfigData['Service_Name']
        username = ConfigData['Username']
        print('Creating systemctl script for ' + service_name)
        content_service = '''"[Unit]\nDescription=Notifications build (.NET Core) running on ubuntu 18.04\n[Service]\nWorkingDirectory=/var/www/''' + 'noti-bin-' + service_name + '''\nExecStart=/usr/bin/dotnet /var/www/''' + 'noti-bin-' + service_name + '''/XM.ID.Invitations.Notifications.dll\nRestart=always\n# Restart service after 2 seconds if the dotnet service crashes:\nRestartSec=2\nKillSignal=SIGINT\nSyslogIdentifier=dotnet-notifications-''' + service_name + '''\nUser=*|User|*\nEnvironment=ASPNETCORE_ENVIRONMENT=Development\nEnvironment=DOTNET_PRINT_TELEMETRY_MESSAGE=false\n[Install]\nWantedBy=multi-user.target"'''
        with cd('/var/www/'):
            run('sudo mkdir -p dummy_notifications')
            run('sudo chown ' + username + ':' + username + ' dummy_notifications/')
            run('sudo rm -rf ' + 'noti-bin-' + service_name)
            run('sudo ln -s dummy_notifications/ ' + 'noti-bin-' + service_name)
            run('sudo chown ' + username + ':' + username + ' noti-bin-' + service_name)
            run('ls -lrt')
            run('sudo mkdir -p SystemLog/Notification/' + service_name)

        content_service = content_service.replace("*|User|*", username)
        cmd = r"sudo sh -c 'echo " + content_service + " > /etc/systemd/system/" + 'kestrel-noti-bin-' + service_name + ".service'"
        run(cmd)
        run('sudo systemctl enable ' + 'kestrel-noti-bin-' + service_name + '.service')
    except Exception as ex:
        print("Notification Service setup failed")
        raise ex

def Sftps3ModuleSetup():
    try:
        global ConfigData
        from fabric.api import env, run
        from fabric.context_managers import cd
        env.host_string = ConfigData['Host']
        env.user = ConfigData['Username']
        env.port = ConfigData['Port']
        env.key_filename = ConfigData['Key_Filename']
        service_name = ConfigData['Service_Name']
        username = ConfigData['Username']
        print('Creating systemctl script for ' + service_name)
        content_service = '''"[Unit]\nDescription=SFTP-S3 build (.NET Core) running on ubuntu 18.04\n[Service]\nWorkingDirectory=/var/www/''' + 'sftps3-bin-' + service_name + '''\nExecStart=/usr/bin/dotnet /var/www/''' + 'sftps3-bin-' + service_name + '''/SFTPToS3Sync.dll\nRestart=always\n# Restart service after 2 seconds if the dotnet service crashes:\nRestartSec=2\nKillSignal=SIGINT\nSyslogIdentifier=dotnet-sftps3-''' + service_name + '''\nUser=*|User|*\nEnvironment=ASPNETCORE_ENVIRONMENT=Development\nEnvironment=DOTNET_PRINT_TELEMETRY_MESSAGE=false\n[Install]\nWantedBy=multi-user.target"'''
        with cd('/var/www/'):
            run('sudo mkdir -p dummy_sftps3')
            run('sudo chown ' + username + ':' + username + ' dummy_sftps3/')
            run('sudo rm -rf ' + 'sftps3-bin-' + service_name)
            run('sudo ln -s dummy_sftps3/ ' + 'sftps3-bin-' + service_name)
            run('sudo chown ' + username + ':' + username + ' sftps3-bin-' + service_name)
            run('ls -lrt')
            run('sudo mkdir -p SystemLog/SFTPS3Sync/' + service_name)
        content_service = content_service.replace("*|User|*", username)
        cmd = r"sudo sh -c 'echo " + content_service + " > /etc/systemd/system/" + 'kestrel-sftps3-bin-' + service_name + ".service'"
        run(cmd)
        run('sudo systemctl enable ' + 'kestrel-sftps3-bin-' + service_name + '.service')
    except Exception as ex:
        print("SFTP-S3 Service setup failed")
        raise ex


def main():
    try:
        print('Starting Invitation Delivery Deployment Script on your system whose operating system is: ' + sys.platform + '\n')
        time.sleep(1)
        print('In total there are 11-17 steps which will be executed to deploy the Invitations Delivery Linux module\n')
        time.sleep(1)
        print('Make sure you have gone through README and correctly configured the config.json file\n')
        time.sleep(2)
        print("Starting in 5 seconds")
        time.sleep(1)
        print("4")
        time.sleep(1)
        print("3")
        time.sleep(1)
        print("2")
        time.sleep(1)
        print("1")
        time.sleep(1)
        global ConfigData, Base_URL
        print('Step1: Checking in your system, if all required libraries required to run this script are already installed\n')
        time.sleep(2)
        InstallDependenciesInTargetServer()
        print('Step2: Verifying the config.json and processing the information\n')
        time.sleep(2)
        GetConfigValues()
        print('Step3: All Seems good so far. Going to login to the server and install the required libraries\n')
        time.sleep(2)
        InstallDotnetInTargetServer()
        print('\nStep4: Lets install Nginx webserver now\n')
        time.sleep(2)
        InstallNginxInTargetServer()
        print('Step5: Now we will be setting up Systemctl script which will monitor the deployed components.\n')
        time.sleep(2)
        ServiceSetupInTargetServer()
        print('\nStep6: Base Installation is done in Target server. Downloading the Source Code now.\n')
        time.sleep(2)
        DownloadInvitationCode()
        print('\nStep7: Code is downloaded successfully. Updating configuration file for Dispatch API\n')
        time.sleep(2)
        print('\nStep8: Publishing build for Dispatch API using Dotnet SDK\n')
        time.sleep(2)
        PublishBuild(r'/WXM-Invitations/XM.ID.Invitations.API/XM.ID.Invitations.API/')
        UpdateAppSettings()

        print('\nStep9: Deploying Dispatch API build to the Server. This might tak few minutes.\n')
        time.sleep(2)
        DeloyBuildOnServer(r'/WXM-Invitations/XM.ID.Invitations.API/XM.ID.Invitations.API/bin/Release/netcoreapp3.1/ubuntu.18.04-x64', 'invitation.zip', 'inv-bin-', ConfigData['Service_Name'])
        print('\nStep10: This would also require changes in Nginx Webserver. Configurting the API Path and certs in Nginx Config.\n')
        time.sleep(2)
        SetupNginx()
        print('\nStep11: Deploying ACM Module and along with this Milestone of deploying Dispatch API is completed. \n')
        time.sleep(2)
        InstallACMModule()

        if ConfigData['NotificationAndReportModule']['InstallNotificationAndReportModule']:
            print("Step12: Setting up Systemctl script for Notifications components\n")
            time.sleep(2)
            NotificationModuleSetup()
            print("Step13: Publishing build for Notifications using Dotnet SDK\n")
            time.sleep(2)
            PublishBuild(r'/WXM-Invitations/XM.ID.Invitations.Notifications/XM.ID.Invitations.Notifications/')
            UpdateNotificationSettings()
            print("Step14: Deploying Notifications build to the Server. This might tak few minutes.\n")
            time.sleep(2)
            DeloyBuildOnServer(r'/WXM-Invitations/XM.ID.Invitations.Notifications/XM.ID.Invitations.Notifications/bin/Release/netcoreapp3.1/ubuntu.18.04-x64', 'notification.zip', 'noti-bin-', ConfigData['Service_Name'])

            print("Step15: Setting up Systemctl script for Reports components\n")
            time.sleep(2)
            ReportModuleSetup()
            ReportDataMergerModuleSetup()
            print("Step16: Publishing build for Reports and DataMerger using Dotnet SDK\n")
            time.sleep(2)
            PublishBuild(r'/WXM-Invitations/DPReporting/DPReporting/')
            UpdateReportsSettings()
            time.sleep(2)
            PublishBuild(r'/WXM-Invitations/WXMInvitationsDataMerger/WXMInvitationsDataMerger/')
            UpdateDataMergerSettings()
            print("Step17: Deploying Reports and DataMerger build to the Server. This might tak few minutes.\n")
            time.sleep(2)
            DeloyBuildOnServer(r'/WXM-Invitations/DPReporting/DPReporting/bin/Release/netcoreapp3.1/ubuntu.18.04-x64', 'reports.zip', 'reports-bin-', ConfigData['Service_Name'])
            
            time.sleep(2)
            DeloyBuildOnServer(r'/WXM-Invitations/WXMInvitationsDataMerger/WXMInvitationsDataMerger/bin/Release/netcoreapp3.1/ubuntu.18.04-x64', 'datamerger.zip', 'datamerger-bin-', ConfigData['Service_Name'])
            print("Deployment is completed on the Target Server. You can verify it now.\n")

        if ConfigData['SftpS3']['InstallsftpS3']:
            print("You are installing another optional feature as SFTP-S3 sync utility. Setting up Systemctl script for this\n")
            time.sleep(2)
            Sftps3ModuleSetup()
            print("Next Step: Publishing build for SFTP-S3 using Dotnet SDK\n")
            time.sleep(2)
            PublishBuild(r'/WXM-Invitations/SFTPToS3Sync/SFTPToS3Sync/')
            UpdateSFTPS3Settings()
            print("Step17: Deploying SFTP-S3 build to the Server. This might tak few minutes.\n")         
            time.sleep(2)
            DeloyBuildOnServer(r'/WXM-Invitations/SFTPToS3Sync/SFTPToS3Sync/bin/Release/netcoreapp3.1/ubuntu.18.04-x64', 'sftps3.zip', 'sftps3-bin-', ConfigData['Service_Name'])
            print("Deployment is completed on the Target Server. You can verify it now.\n")
            
        #Printing deployment results
        print("Base URL: " + Base_URL)
        print("Dispatch API URL: " + Base_URL + '/' + ConfigData['Service_Name'] + '/api/dispatchRequest')
        print('ACM Front-end URL: ' + Base_URL + '/acm/' + ConfigData['Service_Name'])
        print('Notifications Log URL:' + Base_URL + '/files-noti-' + ConfigData['Service_Name'] + '/')
        print('Notifications Username: As mentioned in your config.json')
        print('Notifications Password: As mentioned in your config.json')
        print('Dispatch Service Status Command: ' + 'sudo systemctl status kestrel-inv-bin-' + ConfigData['Service_Name'] + '.service')
        print('Dispatch Service Restart Command: ' + 'sudo systemctl restart kestrel-inv-bin-' + ConfigData['Service_Name'] + '.service')

        #Print only when this addtional setting is enabled
        if ConfigData['NotificationAndReportModule']['InstallNotificationAndReportModule']:
            print('Notification Service Status Command: ' + 'sudo systemctl status kestrel-noti-bin-' + ConfigData['Service_Name'] + '.service')
            print('Notification Service Restart Command: ' + 'sudo systemctl restart kestrel-noti-bin-' + ConfigData['Service_Name'] + '.service')
            print('DPReport Service Status Command: ' + 'sudo systemctl status kestrel-reports-bin-' + ConfigData['Service_Name'] + '.service')
            print('DPReport Service Restart Command: ' + 'sudo systemctl restart kestrel-reports-bin-' + ConfigData['Service_Name'] + '.service')
            print('DataMerger Service Status Command: ' + 'sudo systemctl status kestrel-datamerger-bin-' + ConfigData['Service_Name'] + '.service')
            print('DataMerger Service Restart Command: ' + 'sudo systemctl restart kestrel-datamerger-bin-' + ConfigData['Service_Name'] + '.service')

        #Print only when SFTP-S3 setting is enabled
        if ConfigData['SftpS3']['InstallsftpS3']:
            print('SFTP-S3 Service Status Command: ' + 'sudo systemctl status kestrel-sftp-s3-bin-' + ConfigData['Service_Name'] + '.service')
            print('SFTP-S3 Service Restart Command: ' + 'sudo systemctl restart kestrel-sftp-s3-bin-' + ConfigData['Service_Name'] + '.service')

    except Exception as ex:
        print(":I am here")
        print(str(ex))


if __name__ == "__main__":
    main()
