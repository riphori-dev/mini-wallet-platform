CREATE USER wallet_app WITH PASSWORD 'wallet_local_only';
CREATE USER users_app WITH PASSWORD 'users_local_only';
CREATE DATABASE wallet OWNER wallet_app;
CREATE DATABASE users OWNER users_app;
REVOKE ALL ON DATABASE wallet FROM PUBLIC;
REVOKE ALL ON DATABASE users FROM PUBLIC;

