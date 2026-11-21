-- Runs once, when the database volume is created.
-- The app gets its own account instead of the postgres superuser (least privilege). It owns only the
-- vulntracker database, so it can run migrations there, but it can't read other databases, create
-- roles, or use superuser features such as running shell commands with COPY ... PROGRAM.
-- The password is a local dev value. In a real deployment it is set separately.
CREATE ROLE vulntracker LOGIN PASSWORD 'vulntracker-dev' NOSUPERUSER NOCREATEDB NOCREATEROLE;
ALTER DATABASE vulntracker OWNER TO vulntracker;
